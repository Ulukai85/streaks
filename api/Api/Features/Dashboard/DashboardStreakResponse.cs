namespace Api.Features.Dashboard;

public record DashboardStreakResponse(int Length, bool IsAlive, DateOnly? LastCompletedPeriod);
