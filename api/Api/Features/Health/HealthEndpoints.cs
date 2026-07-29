using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health");

        group.MapGet("/", async Task<Results<Ok<HealthResponse>, ProblemHttpResult>> (AppDbContext db, CancellationToken ct) =>
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
