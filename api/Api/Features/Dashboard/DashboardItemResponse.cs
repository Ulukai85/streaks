namespace Api.Features.Dashboard;

public record DashboardItemResponse(
    Guid Id,
    string Name,
    string? Url,
    string Cadence,
    string Color,
    int SortOrder,
    DashboardStreakResponse Streak);
