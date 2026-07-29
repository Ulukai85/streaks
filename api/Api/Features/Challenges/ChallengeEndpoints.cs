using Api.Data;
using Api.Domain;
using Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Challenges;

public static class ChallengeEndpoints
{
    public static void MapChallengeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/challenges").RequireAuthorization();

        group.MapPost("/", async (
            CreateChallengeRequest request,
            AppDbContext db,
            ICurrentUserProvider currentUser,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == currentUser.UserId, ct);
            var cadence = Enum.Parse<Cadence>(request.Cadence, ignoreCase: true);

            var maxSortOrder = await db.Challenges
                .Where(c => c.UserId == currentUser.UserId)
                .Select(c => (int?)c.SortOrder)
                .MaxAsync(ct);

            var url = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url;

            var challenge = new Challenge
            {
                Id = Guid.NewGuid(),
                UserId = currentUser.UserId,
                Name = request.Name,
                Url = url,
                Cadence = cadence,
                TargetCount = 1,
                StartsOn = PeriodCalculator.PeriodStartFor(timeProvider.GetUtcNow(), user.TimeZoneId, cadence),
                Color = request.Color,
                SortOrder = request.SortOrder ?? (maxSortOrder is null ? 0 : maxSortOrder.Value + 1),
            };

            db.Challenges.Add(challenge);
            await db.SaveChangesAsync(ct);

            var response = ToResponse(challenge);
            return TypedResults.Created($"/api/challenges/{response.Id}", response);
        }).AddEndpointFilter<ValidationFilter<CreateChallengeRequest>>();

        group.MapGet("/", async (AppDbContext db, ICurrentUserProvider currentUser, CancellationToken ct) =>
        {
            var challenges = await db.Challenges
                .AsNoTracking()
                .Where(c => c.UserId == currentUser.UserId && c.ArchivedAt == null)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(ct);

            return TypedResults.Ok(challenges.Select(ToResponse).ToList());
        });

        group.MapPost("/{id:guid}/archive", async Task<Results<Ok<ChallengeResponse>, NotFound>> (
            Guid id,
            AppDbContext db,
            ICurrentUserProvider currentUser,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            // Tracked query, deliberately not AsNoTracking(): this load feeds a possible
            // mutation of ArchivedAt below, unlike every other read path in this file.
            var challenge = await db.Challenges
                .SingleOrDefaultAsync(c => c.Id == id && c.UserId == currentUser.UserId, ct);

            if (challenge is null)
            {
                return TypedResults.NotFound();
            }

            if (challenge.ArchivedAt is null)
            {
                challenge.ArchivedAt = timeProvider.GetUtcNow();
                await db.SaveChangesAsync(ct);
            }

            return TypedResults.Ok(ToResponse(challenge));
        });
    }

    private static ChallengeResponse ToResponse(Challenge c) => new(
        c.Id, c.Name, c.Url, c.Cadence.ToString(), c.TargetCount, c.StartsOn, c.ArchivedAt, c.Color, c.SortOrder);
}
