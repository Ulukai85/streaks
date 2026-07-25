using System.Globalization;

namespace Api.Domain;

public static class PeriodCalculator
{
    public static DateOnly PeriodStartFor(DateTimeOffset instant, string timeZoneId, Cadence cadence)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(instant, tz);
        
        var today = DateOnly.FromDateTime(local.DateTime);

        return cadence switch
        {
            Cadence.Daily => today,
            Cadence.Weekly => ISOWeek.ToDateOnly(ISOWeek.GetYear(today), ISOWeek.GetWeekOfYear(today), DayOfWeek.Monday),
            Cadence.Monthly => new DateOnly(today.Year, today.Month, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), $"Unsupported cadence: {cadence}")
        };
    }
}