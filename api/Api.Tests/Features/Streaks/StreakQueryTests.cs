using Api.Domain;

namespace Api.Tests.Features.Streaks;

public class StreakQueryTests
{
    // Independent transcription of the ordinal arithmetic in StreakQuery's raw SQL
    // (Api/Features/Streaks/StreakQuery.cs) - deliberately not a call into PeriodOrdinal.For.
    // The point of this test is to catch the C# and SQL formulas drifting apart, so this
    // must stay a second, handwritten implementation rather than reuse the production one.
    private static int SqlOrdinal(DateOnly periodStart, Cadence cadence)
    {
        // "PeriodStart" - DATE '1970-01-01'
        var daysSinceEpoch = periodStart.DayNumber - new DateOnly(1970, 1, 1).DayNumber; 
        
        return cadence switch
        {
            Cadence.Daily => daysSinceEpoch,
            Cadence.Weekly => daysSinceEpoch / 7,
            Cadence.Monthly => (periodStart.Year * 12) + periodStart.Month,
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), $"Unsupported cadence: {cadence}")
        };
    }

    [Theory]
    [InlineData(1970, 1, 1, Cadence.Daily)]
    [InlineData(2026, 7, 26, Cadence.Daily)]
    [InlineData(2028, 2, 29, Cadence.Daily)] // leap day
    [InlineData(2099, 12, 31, Cadence.Daily)]
    [InlineData(2026, 7, 20, Cadence.Weekly)] // ordinary Monday
    [InlineData(2026, 12, 28, Cadence.Weekly)] // Monday of ISO 2026-W53
    [InlineData(2027, 1, 4, Cadence.Weekly)] // Monday of ISO 2027-W01 - the week right after 2026-W53
    [InlineData(2028, 2, 28, Cadence.Weekly)] // Monday in a leap-year February
    [InlineData(2026, 7, 1, Cadence.Monthly)]
    [InlineData(2026, 12, 1, Cadence.Monthly)]
    [InlineData(2027, 1, 1, Cadence.Monthly)] // year boundary
    [InlineData(2028, 2, 1, Cadence.Monthly)] // leap-year February
    public void PeriodOrdinal_matches_hand_transcribed_sql_arithmetic(int year, int month, int day, Cadence cadence)
    {
        var periodStart = new DateOnly(year, month, day);

        var csharpOrdinal = PeriodOrdinal.For(periodStart, cadence);
        var sqlOrdinal = SqlOrdinal(periodStart, cadence);

        csharpOrdinal.Should().Be(sqlOrdinal);
    }
}
