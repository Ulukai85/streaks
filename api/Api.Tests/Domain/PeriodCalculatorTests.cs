using Api.Domain;

namespace Api.Tests.Domain;

public class PeriodCalculatorTests
{
    // The CET<->CEST cases below deliberately straddle the exact transition instant (01:00 UTC),
    // not just "sometime near it" - Berlin's transition happens at 2am local, nowhere near
    // midnight, so no instant here can make Daily cadence return a *different* DateOnly whether
    // the offset resolution is right or wrong. What these cases still prove: the conversion
    // resolves cleanly (no exception, no silently-wrong day) for the instant immediately before
    // and immediately after the offset changes, including the skipped/repeated local hour itself.
    public static TheoryData<string, DateTimeOffset, string, Cadence, DateOnly> PeriodStartForData =>
        new()
        {
            {
                "Spring: instant just before CET->CEST (00:59 UTC = 01:59 CET)",
                new DateTimeOffset(2026, 3, 29, 0, 59, 0, TimeSpan.Zero),
                "Europe/Berlin",
                Cadence.Daily,
                new DateOnly(2026, 3, 29)
            },
            {
                "Spring: instant just after CET->CEST (01:00 UTC = 03:00 CEST, the skipped hour)",
                new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero),
                "Europe/Berlin",
                Cadence.Daily,
                new DateOnly(2026, 3, 29)
            },
            {
                "Fall: instant just before CEST->CET (00:59 UTC = 02:59 CEST, first pass of the repeated hour)",
                new DateTimeOffset(2026, 10, 25, 0, 59, 0, TimeSpan.Zero),
                "Europe/Berlin",
                Cadence.Daily,
                new DateOnly(2026, 10, 25)
            },
            {
                "Fall: instant just after CEST->CET (01:00 UTC = 02:00 CET, second pass of the repeated hour)",
                new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero),
                "Europe/Berlin",
                Cadence.Daily,
                new DateOnly(2026, 10, 25)
            },
            {
                "ISO week spanning the year boundary",
                new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero),
                "Europe/Berlin",
                Cadence.Weekly,
                new DateOnly(2026, 12, 28)
            },
            {
                "00:30 local time in Berlin - must resolve to today local, not yesterday UTC",
                new DateTimeOffset(2026, 6, 15, 23, 30, 0, TimeSpan.Zero),
                "Europe/Berlin",
                Cadence.Daily,
                new DateOnly(2026, 6, 16)
            },
            {
                "23:30 local time in Berlin - must resolve to today, not tomorrow UTC",
                new DateTimeOffset(2026, 6, 15, 21, 30, 0, TimeSpan.Zero),
                "Europe/Berlin",
                Cadence.Daily,
                new DateOnly(2026, 6, 15)
            },
            {
                "Monthly cadence: instant crossing local midnight from 31 Jan into 1 Feb",
                new DateTimeOffset(2026, 1, 31, 23, 0, 0, TimeSpan.Zero),
                "Europe/Berlin",
                Cadence.Monthly,
                new DateOnly(2026, 2, 1)
            },
            {
                "Non-European zone",
                new DateTimeOffset(2026, 6, 15, 21, 30, 0, TimeSpan.Zero),
                "Pacific/Auckland",
                Cadence.Daily,
                new DateOnly(2026, 6, 16)
            },
        };

    [Theory]
    [MemberData(nameof(PeriodStartForData))]
    public void PeriodStartFor_ReturnsCorrectDate(string because, DateTimeOffset instant, string timeZoneId, Cadence cadence, DateOnly expected)
    {
        var result = PeriodCalculator.PeriodStartFor(instant, timeZoneId, cadence);
        
        result.Should().Be(expected, because);
    }
    
}