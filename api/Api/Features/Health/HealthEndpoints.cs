using Api.Data;

namespace Api.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health");

        group.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
        {
            var connected = await db.Database.CanConnectAsync(ct);
            return connected
                ? Results.Ok(new HealthResponse("ok", true))
                : Results.Problem(
                    title: "Service unavailable",
                    detail: "Database connection failed.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });
    }
}
