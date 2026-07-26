namespace Api.Domain;

public static class PeriodUrgency
{
    // Two challenges of the same cadence always get the same value on a given request - this
    // only discriminates between cadences present in a response; same-cadence ordering is
    // entirely the caller's tie-break.
    public static int DaysRemaining(DateOnly periodStart, DateOnly today, Cadence cadence)
    {
        var periodEnd = cadence switch
        {
            Cadence.Daily => periodStart,
            Cadence.Weekly => periodStart.AddDays(6),
            Cadence.Monthly => new DateOnly(periodStart.Year, periodStart.Month, 1).AddMonths(1).AddDays(-1),
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), $"Unsupported cadence: {cadence}")
        };

        return periodEnd.DayNumber - today.DayNumber;
    }
}
