namespace Api.Features.Health;

public record HealthResponse(string Status, bool DatabaseConnected);
