using Api.Domain;

namespace Api.Tests.Domain;

public class PeriodUrgencyTests
{
    [Theory]
    [InlineData(2026, 7, 26)]
    [InlineData(2026, 12, 31)]
    [InlineData(2028, 2, 29)]
    public void Daily_Always_Returns_Zero(int year, int month, int day)
    {
        var today = new DateOnly(year, month, day);

        PeriodUrgency.DaysRemaining(today, today, Cadence.Daily).Should().Be(0);
    }

    [Fact]
    public void Weekly_Monday_Today_Returns_Six()
    {
        var monday = new DateOnly(2026, 7, 20);

        PeriodUrgency.DaysRemaining(monday, monday, Cadence.Weekly).Should().Be(6);
    }

    [Fact]
    public void Weekly_MidWeek_Today_Returns_Three()
    {
        var monday = new DateOnly(2026, 7, 20);
        var thursday = monday.AddDays(3);

        PeriodUrgency.DaysRemaining(monday, thursday, Cadence.Weekly).Should().Be(3);
    }

    [Fact]
    public void Weekly_Sunday_Today_Returns_Zero()
    {
        var monday = new DateOnly(2026, 7, 20);
        var sunday = monday.AddDays(6);

        PeriodUrgency.DaysRemaining(monday, sunday, Cadence.Weekly).Should().Be(0);
    }

    [Fact]
    public void Monthly_FirstDayOfMonth_Returns_ThirtyForJuly()
    {
        var firstOfJuly = new DateOnly(2026, 7, 1); // 31-day month

        PeriodUrgency.DaysRemaining(firstOfJuly, firstOfJuly, Cadence.Monthly).Should().Be(30);
    }

    [Fact]
    public void Monthly_LastDayOfMonth_Returns_Zero()
    {
        var firstOfJuly = new DateOnly(2026, 7, 1);
        var lastOfJuly = new DateOnly(2026, 7, 31);

        PeriodUrgency.DaysRemaining(firstOfJuly, lastOfJuly, Cadence.Monthly).Should().Be(0);
    }

    [Fact]
    public void Monthly_February_NonLeapYear_Returns_TwentySeven()
    {
        var firstOfFeb2026 = new DateOnly(2026, 2, 1); // 2026 is not a leap year

        PeriodUrgency.DaysRemaining(firstOfFeb2026, firstOfFeb2026, Cadence.Monthly).Should().Be(27);
    }

    [Fact]
    public void Monthly_February_LeapYear_Returns_TwentyEight()
    {
        var firstOfFeb2028 = new DateOnly(2028, 2, 1); // 2028 is a leap year

        PeriodUrgency.DaysRemaining(firstOfFeb2028, firstOfFeb2028, Cadence.Monthly).Should().Be(28);
    }
}
