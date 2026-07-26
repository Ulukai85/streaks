namespace Api.Features.Dashboard;

public record DashboardResponse(
    IReadOnlyList<DashboardItemResponse> Open,
    IReadOnlyList<DashboardItemResponse> DoneThisPeriod);
