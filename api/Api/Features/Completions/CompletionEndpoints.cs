using Api.Data;
using Api.Domain;
using Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Completions;

public static class CompletionEndpoints
{
    public static void MapCompletionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/challenges");

        group.MapPost("/{challengeId:guid}/completions", async Task<Results<Created<CompletionResponse>, NotFound, ProblemHttpResult>> (
            Guid challengeId,
            CompleteChallengeRequest request,
            AppDbContext db,
            ICurrentUserProvider currentUser,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var challenge = await db.Challenges.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == challengeId && c.UserId == currentUser.UserId, ct);

            if (challenge is null)
            {
                return TypedResults.NotFound();
            }

            if (challenge.ArchivedAt is not null)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Challenge is archived",
                    detail: "Archived challenges cannot record new completions.");
            }

            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == currentUser.UserId, ct);
            var currentPeriodStart = PeriodCalculator.PeriodStartFor(timeProvider.GetUtcNow(), user.TimeZoneId, challenge.Cadence);
            var periodStart = request.PeriodStart ?? currentPeriodStart;

            if (request.PeriodStart is { } given && !IsCanonicalPeriodStart(given, challenge.Cadence))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid period",
                    detail: "PeriodStart must be the canonical start of its period (a Monday for weekly, the 1st for monthly).");
            }

            var periodsAgo = PeriodOrdinal.For(currentPeriodStart, challenge.Cadence) - PeriodOrdinal.For(periodStart, challenge.Cadence);
            if (periodsAgo is < 0 or > 2)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid period",
                    detail: "PeriodStart must be within the current period or the previous two.");
            }

            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note;

            var completion = new Completion
            {
                Id = Guid.NewGuid(),
                ChallengeId = challengeId,
                PeriodStart = periodStart,
                CompletedAt = timeProvider.GetUtcNow(),
                Note = note,
            };

            db.Completions.Add(completion);
            await db.SaveChangesAsync(ct);

            var response = ToResponse(completion);
            return TypedResults.Created($"/api/challenges/{challengeId}/completions/{response.Id}", response);
        }).AddEndpointFilter<ValidationFilter<CompleteChallengeRequest>>();
    }

    private static bool IsCanonicalPeriodStart(DateOnly periodStart, Cadence cadence) => cadence switch
    {
        Cadence.Daily => true,
        Cadence.Weekly => periodStart.DayOfWeek == DayOfWeek.Monday,
        Cadence.Monthly => periodStart.Day == 1,
        _ => throw new ArgumentOutOfRangeException(nameof(cadence), $"Unsupported cadence: {cadence}")
    };

    private static CompletionResponse ToResponse(Completion c) => new(c.Id, c.ChallengeId, c.PeriodStart, c.CompletedAt, c.Note);
}
