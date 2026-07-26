using Api.Domain;

namespace Api.Tests.Domain;

public class PeriodOrdinalTests
{
    [Fact]
    public void Daily_ConsecutiveDays_DifferByOne()
    {
        var today = PeriodOrdinal.For(new DateOnly(2026, 6, 16), Cadence.Daily);
        var yesterday = PeriodOrdinal.For(new DateOnly(2026, 6, 15), Cadence.Daily);

        (today - yesterday).Should().Be(1);
    }

    [Fact]
    public void Weekly_ConsecutiveMondays_DifferByOne()
    {
        var thisMonday = PeriodOrdinal.For(new DateOnly(2026, 6, 15), Cadence.Weekly);
        var lastMonday = PeriodOrdinal.For(new DateOnly(2026, 6, 8), Cadence.Weekly);

        (thisMonday - lastMonday).Should().Be(1);
    }

    [Fact]
    public void Weekly_MondaysAcrossYearBoundary_DifferByOne()
    {
        var jan2027 = PeriodOrdinal.For(new DateOnly(2027, 1, 4), Cadence.Weekly);
        var dec2026 = PeriodOrdinal.For(new DateOnly(2026, 12, 28), Cadence.Weekly);

        (jan2027 - dec2026).Should().Be(1);
    }

    [Fact]
    public void Monthly_ConsecutiveMonths_DifferByOne()
    {
        var jan2027 = PeriodOrdinal.For(new DateOnly(2027, 1, 1), Cadence.Monthly);
        var dec2026 = PeriodOrdinal.For(new DateOnly(2026, 12, 1), Cadence.Monthly);

        (jan2027 - dec2026).Should().Be(1);
    }

    [Theory]
    [InlineData(Cadence.Daily)]
    [InlineData(Cadence.Weekly)]
    [InlineData(Cadence.Monthly)]
    public void SamePeriodStart_DiffersByZero(Cadence cadence)
    {
        var periodStart = new DateOnly(2026, 6, 15);

        (PeriodOrdinal.For(periodStart, cadence) - PeriodOrdinal.For(periodStart, cadence)).Should().Be(0);
    }
}
