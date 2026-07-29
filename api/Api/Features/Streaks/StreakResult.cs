namespace Api.Features.Streaks;

public sealed record StreakResult(int Length, bool IsAlive, DateOnly? LastCompletedPeriod);
