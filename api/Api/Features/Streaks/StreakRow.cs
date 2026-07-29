namespace Api.Features.Streaks;

public sealed record StreakRow(int Length, int EndsAt, DateOnly LastPeriodStart);
