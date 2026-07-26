namespace Api.Domain;

public static class PeriodOrdinal
{
    private static readonly DateOnly Epoch = new(1970, 1, 1);

    public static int For(DateOnly periodStart, Cadence cadence) => cadence switch
    {
        Cadence.Daily => periodStart.DayNumber - Epoch.DayNumber,
        Cadence.Weekly => (periodStart.DayNumber - Epoch.DayNumber) / 7,
        Cadence.Monthly => (periodStart.Year * 12) + periodStart.Month,
        _ => throw new ArgumentOutOfRangeException(nameof(cadence), $"Unsupported cadence: {cadence}")
    };
}
