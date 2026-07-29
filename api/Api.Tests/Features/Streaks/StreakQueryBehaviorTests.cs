using Api.Domain;
using Api.Features.Streaks;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Features.Streaks;

public class StreakQueryBehaviorTests(PostgresFixture postgres, ApiFactory factory) : IntegrationTestBase(postgres, factory)
{
    // "Today" here means "the current period's start" (already period-resolved), matching how
    // StreakQuery.ForChallenge's `today` parameter is meant to be called - see Api/Features/Streaks/StreakQuery.cs.
    private static readonly Dictionary<Cadence, DateOnly> ReferenceToday = new()
    {
        [Cadence.Daily] = new DateOnly(2026, 7, 26),
        [Cadence.Weekly] = new DateOnly(2026, 7, 20), // a Monday
        [Cadence.Monthly] = new DateOnly(2026, 7, 1),
    };

    private static DateOnly PeriodsAgo(Cadence cadence, int count) => cadence switch
    {
        Cadence.Daily => ReferenceToday[cadence].AddDays(-count),
        Cadence.Weekly => ReferenceToday[cadence].AddDays(-7 * count),
        Cadence.Monthly => ReferenceToday[cadence].AddMonths(-count),
        _ => throw new ArgumentOutOfRangeException(nameof(cadence), $"Unsupported cadence: {cadence}")
    };

    private static DateTimeOffset AsInstant(DateOnly periodStart) => new(periodStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private async Task<StreakResult> ForChallengeAsync(
        Guid challengeId, Cadence cadence, DateOnly startsOn, DateOnly? archivedAtLocalDateOnly, DateOnly today)
    {
        await using var db = CreateDbContext();
        return await new StreakQuery(db).ForChallenge(challengeId, cadence, startsOn, archivedAtLocalDateOnly, today, CancellationToken.None);
    }

    [Theory]
    [InlineData(Cadence.Daily)]
    [InlineData(Cadence.Weekly)]
    [InlineData(Cadence.Monthly)]
    public async Task Alive_When_Current_Period_Is_Completed(Cadence cadence)
    {
        var today = ReferenceToday[cadence];
        var startsOn = PeriodsAgo(cadence, 20);
        var challengeId = await SeedChallengeAsync(cadence, startsOn);
        await SeedCompletionAsync(challengeId, today);

        var result = await ForChallengeAsync(challengeId, cadence, startsOn, null, today);

        result.IsAlive.Should().BeTrue();
        result.Length.Should().Be(1);
        result.LastCompletedPeriod.Should().Be(today);
    }

    [Theory]
    [InlineData(Cadence.Daily)]
    [InlineData(Cadence.Weekly)]
    [InlineData(Cadence.Monthly)]
    public async Task Alive_When_Previous_Period_Completed_And_Current_Period_Still_Open(Cadence cadence)
    {
        var today = ReferenceToday[cadence];
        var startsOn = PeriodsAgo(cadence, 20);
        var challengeId = await SeedChallengeAsync(cadence, startsOn);
        await SeedCompletionAsync(challengeId, PeriodsAgo(cadence, 1));

        var result = await ForChallengeAsync(challengeId, cadence, startsOn, null, today);

        result.IsAlive.Should().BeTrue();
        result.Length.Should().Be(1);
        result.LastCompletedPeriod.Should().Be(PeriodsAgo(cadence, 1));
    }

    [Theory]
    [InlineData(Cadence.Daily)]
    [InlineData(Cadence.Weekly)]
    [InlineData(Cadence.Monthly)]
    public async Task Dead_When_Last_Completion_Is_Two_Or_More_Periods_Ago(Cadence cadence)
    {
        var today = ReferenceToday[cadence];
        var startsOn = PeriodsAgo(cadence, 20);
        var challengeId = await SeedChallengeAsync(cadence, startsOn);
        await SeedCompletionAsync(challengeId, PeriodsAgo(cadence, 2));

        var result = await ForChallengeAsync(challengeId, cadence, startsOn, null, today);

        result.IsAlive.Should().BeFalse();
        result.Length.Should().Be(1);
        result.LastCompletedPeriod.Should().Be(PeriodsAgo(cadence, 2));
    }

    [Theory]
    [InlineData(Cadence.Daily)]
    [InlineData(Cadence.Weekly)]
    [InlineData(Cadence.Monthly)]
    public async Task Not_Reported_As_Broken_For_A_Brand_New_Challenge_With_No_Completions(Cadence cadence)
    {
        var today = ReferenceToday[cadence];
        var challengeId = await SeedChallengeAsync(cadence, startsOn: today);

        var result = await ForChallengeAsync(challengeId, cadence, today, null, today);

        result.Length.Should().Be(0);
        result.IsAlive.Should().BeFalse();
        result.LastCompletedPeriod.Should().BeNull();
    }

    [Theory]
    [InlineData(Cadence.Daily)]
    [InlineData(Cadence.Weekly)]
    [InlineData(Cadence.Monthly)]
    public async Task Archived_Challenge_Freezes_As_Alive_When_Last_Completion_Was_Right_Before_Archiving(Cadence cadence)
    {
        // Archived 5 periods before ReferenceToday - a real "today" that far past the archival
        // moment proves the freeze uses ArchivedAt, not the clock: evaluated against ReferenceToday
        // this would clearly read as dead.
        var archivedAtPeriod = PeriodsAgo(cadence, 5);
        var lastCompletion = PeriodsAgo(cadence, 6); // the period right before archiving - the grace case
        var startsOn = PeriodsAgo(cadence, 20);
        var challengeId = await SeedChallengeAsync(cadence, startsOn, archivedAt: AsInstant(archivedAtPeriod));
        await SeedCompletionAsync(challengeId, lastCompletion);

        var result = await ForChallengeAsync(challengeId, cadence, startsOn, archivedAtPeriod, ReferenceToday[cadence]);

        result.IsAlive.Should().BeTrue();
        result.Length.Should().Be(1);
        result.LastCompletedPeriod.Should().Be(lastCompletion);
    }

    [Theory]
    [InlineData(Cadence.Daily)]
    [InlineData(Cadence.Weekly)]
    [InlineData(Cadence.Monthly)]
    public async Task Archived_Challenge_Is_Dead_When_The_Gap_Already_Existed_Before_Archiving(Cadence cadence)
    {
        var archivedAtPeriod = PeriodsAgo(cadence, 5);
        var lastCompletion = PeriodsAgo(cadence, 10); // gap predates archiving, not just the passage of real time
        var startsOn = PeriodsAgo(cadence, 20);
        var challengeId = await SeedChallengeAsync(cadence, startsOn, archivedAt: AsInstant(archivedAtPeriod));
        await SeedCompletionAsync(challengeId, lastCompletion);

        var result = await ForChallengeAsync(challengeId, cadence, startsOn, archivedAtPeriod, ReferenceToday[cadence]);

        result.IsAlive.Should().BeFalse();
        result.Length.Should().Be(1);
        result.LastCompletedPeriod.Should().Be(lastCompletion);
    }

    // Regression test for the LastCompletedPeriod bug: an earlier draft picked MAX("CompletedAt")
    // across the island and derived a period from that instant. Retroactive completion means
    // CompletedAt order and PeriodStart order can diverge - here the older period is backfilled
    // *after* the newer one was completed on time, which broke that approach.
    [Theory]
    [InlineData(Cadence.Daily)]
    [InlineData(Cadence.Weekly)]
    [InlineData(Cadence.Monthly)]
    public async Task LastCompletedPeriod_Is_The_Latest_PeriodStart_Even_When_An_Earlier_Period_Was_Backfilled_Later(Cadence cadence)
    {
        var today = ReferenceToday[cadence];
        var previous = PeriodsAgo(cadence, 1);
        var startsOn = PeriodsAgo(cadence, 20);
        var challengeId = await SeedChallengeAsync(cadence, startsOn);

        await SeedCompletionAsync(challengeId, today, completedAt: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedCompletionAsync(challengeId, previous, completedAt: new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var result = await ForChallengeAsync(challengeId, cadence, startsOn, null, today);

        result.Length.Should().Be(2);
        result.LastCompletedPeriod.Should().Be(today);
    }
}
