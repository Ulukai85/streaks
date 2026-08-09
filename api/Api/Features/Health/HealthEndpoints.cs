using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health");

        // HEAD alongside GET: some uptime-checker free tiers (e.g. Better
        // Stack) only offer HEAD for their basic monitor, and MapGet alone
        // doesn't implicitly answer HEAD requests (confirmed empirically —
        // it 405s without this).
        group.MapMethods("/", [HttpMethods.Get, HttpMethods.Head], async Task<Results<Ok<HealthResponse>, ProblemHttpResult>> (AppDbContext db, CancellationToken ct) =>
        {
            var connected = await db.Database.CanConnectAsync(ct);
            return connected
                ? TypedResults.Ok(new HealthResponse("ok", true))
                : TypedResults.Problem(
                    title: "Service unavailable",
                    detail: "Database connection failed.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });
    }
}
