using Api.Data;
using Api.Domain;
using Api.Features.Streaks;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard");

        group.MapGet("/", async (
            AppDbContext db,
            ICurrentUserProvider currentUser,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == currentUser.UserId, ct);
            var now = timeProvider.GetUtcNow();
            var today = PeriodCalculator.PeriodStartFor(now, user.TimeZoneId, Cadence.Daily);

            var challenges = await db.Challenges.AsNoTracking()
                .Where(c => c.UserId == currentUser.UserId && c.ArchivedAt == null && c.StartsOn <= today)
                .ToListAsync(ct);

            // At most 3 distinct cadences -> at most 3 PeriodCalculator calls, not one per challenge.
            var periodStartByCadence = challenges
                .Select(c => c.Cadence)
                .Distinct()
                .ToDictionary(cadence => cadence, cadence => PeriodCalculator.PeriodStartFor(now, user.TimeZoneId, cadence));

            var challengeIds = challenges.Select(c => c.Id).ToList();
            var distinctPeriodStarts = periodStartByCadence.Values.Distinct().ToList();

            // Conservative cross-product filter (ChallengeId IN (...) AND PeriodStart IN (...)),
            // not a precise per-challenge tuple match - EF/LINQ doesn't translate a tuple-pair
            // list cleanly. Correctness is unaffected: the actual "is done" check below is
            // still an exact (ChallengeId, PeriodStart) match against this set.
            var completedThisPeriod = (await db.Completions.AsNoTracking()
                    .Where(comp => challengeIds.Contains(comp.ChallengeId) && distinctPeriodStarts.Contains(comp.PeriodStart))
                    .Select(comp => new { comp.ChallengeId, comp.PeriodStart })
                    .ToListAsync(ct))
                .Select(x => (x.ChallengeId, x.PeriodStart))
                .ToHashSet();

            var open = new List<(DashboardItemResponse Item, int DaysRemaining)>();
            var done = new List<DashboardItemResponse>();

            foreach (var challenge in challenges)
            {
                // StreakQuery.ForChallenge's `today` param means "current period start", not the
                // calendar date - see StreakQueryBehaviorTests.ReferenceToday's comment.
                var currentPeriodStart = periodStartByCadence[challenge.Cadence];

                var streak = await new StreakQuery(db).ForChallenge(
                    challenge.Id, challenge.Cadence, challenge.StartsOn,
                    archivedAtLocalDateOnly: null, currentPeriodStart, ct);

                var item = ToResponse(challenge, streak);

                if (completedThisPeriod.Contains((challenge.Id, currentPeriodStart)))
                {
                    done.Add(item);
                }
                else
                {
                    var daysRemaining = PeriodUrgency.DaysRemaining(currentPeriodStart, today, challenge.Cadence);
                    open.Add((item, daysRemaining));
                }
            }

            var orderedOpen = open
                .OrderBy(x => x.DaysRemaining)
                .ThenBy(x => x.Item.SortOrder)
                .Select(x => x.Item)
                .ToList();

            var orderedDone = done.OrderBy(i => i.SortOrder).ToList();

            return TypedResults.Ok(new DashboardResponse(orderedOpen, orderedDone));
        });
    }

    private static DashboardItemResponse ToResponse(Challenge c, StreakResult streak) => new(
        c.Id, c.Name, c.Url, c.Cadence.ToString(), c.Color, c.SortOrder,
        new DashboardStreakResponse(streak.Length, streak.IsAlive, streak.LastCompletedPeriod));
}
