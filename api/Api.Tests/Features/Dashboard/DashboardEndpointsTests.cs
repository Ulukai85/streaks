using System.Net.Http.Json;
using Api.Domain;
using Api.Features.Dashboard;
using Api.Features.Streaks;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Features.Dashboard;

public class DashboardEndpointsTests(PostgresFixture postgres, ApiFactory factory) : IntegrationTestBase(postgres, factory)
{
    private async Task<string> UserTimeZoneAsync()
    {
        await using var db = CreateDbContext();
        return (await db.Users.AsNoTracking().SingleAsync()).TimeZoneId;
    }

    private async Task<DateOnly> CurrentPeriodStartAsync(Cadence cadence) =>
        PeriodCalculator.PeriodStartFor(DateTimeOffset.UtcNow, await UserTimeZoneAsync(), cadence);

    private async Task<Guid> SeedChallengeAsync(
        Cadence cadence, DateOnly startsOn, int sortOrder = 0, DateTimeOffset? archivedAt = null)
    {
        await using var db = CreateDbContext();
        var userId = (await db.Users.AsNoTracking().SingleAsync()).Id;

        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = $"Dashboard test {Guid.NewGuid()}",
            Cadence = cadence,
            TargetCount = 1,
            StartsOn = startsOn,
            ArchivedAt = archivedAt,
            Color = "blue",
            SortOrder = sortOrder,
        };
        db.Challenges.Add(challenge);
        await db.SaveChangesAsync();
        return challenge.Id;
    }

    private async Task SeedCompletionAsync(Guid challengeId, DateOnly periodStart)
    {
        await using var db = CreateDbContext();
        db.Completions.Add(new Completion
        {
            Id = Guid.NewGuid(),
            ChallengeId = challengeId,
            PeriodStart = periodStart,
            CompletedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_Returns_Empty_Groups_When_No_Challenges()
    {
        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");

        response.Should().NotBeNull();
        response!.Open.Should().BeEmpty();
        response.DoneThisPeriod.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_Excludes_Archived_Challenges()
    {
        var today = await CurrentPeriodStartAsync(Cadence.Daily);
        var archivedNoCompletion = await SeedChallengeAsync(Cadence.Daily, today, archivedAt: DateTimeOffset.UtcNow);
        var archivedWithCompletion = await SeedChallengeAsync(Cadence.Daily, today, archivedAt: DateTimeOffset.UtcNow);
        await SeedCompletionAsync(archivedWithCompletion, today);

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");

        response!.Open.Should().NotContain(i => i.Id == archivedNoCompletion || i.Id == archivedWithCompletion);
        response.DoneThisPeriod.Should().NotContain(i => i.Id == archivedNoCompletion || i.Id == archivedWithCompletion);
    }

    [Fact]
    public async Task Get_Excludes_Challenges_Not_Yet_Started()
    {
        var today = await CurrentPeriodStartAsync(Cadence.Daily);
        var notStartedYet = await SeedChallengeAsync(Cadence.Daily, today.AddDays(1));

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");

        response!.Open.Should().NotContain(i => i.Id == notStartedYet);
        response.DoneThisPeriod.Should().NotContain(i => i.Id == notStartedYet);
    }

    [Fact]
    public async Task Get_Puts_Challenge_Without_CurrentPeriod_Completion_In_Open()
    {
        var today = await CurrentPeriodStartAsync(Cadence.Daily);
        var challengeId = await SeedChallengeAsync(Cadence.Daily, today);

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");

        response!.Open.Should().ContainSingle(i => i.Id == challengeId);
        response.DoneThisPeriod.Should().NotContain(i => i.Id == challengeId);
    }

    [Fact]
    public async Task Get_Puts_Challenge_Completed_ThisPeriod_In_DoneThisPeriod()
    {
        var today = await CurrentPeriodStartAsync(Cadence.Daily);
        var challengeId = await SeedChallengeAsync(Cadence.Daily, today);
        await SeedCompletionAsync(challengeId, today);

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");

        response!.DoneThisPeriod.Should().ContainSingle(i => i.Id == challengeId);
        response.Open.Should().NotContain(i => i.Id == challengeId);
    }

    [Fact]
    public async Task Get_Puts_Challenge_Completed_Only_LastPeriod_In_Open()
    {
        var currentMonday = await CurrentPeriodStartAsync(Cadence.Weekly);
        var lastMonday = currentMonday.AddDays(-7);
        var challengeId = await SeedChallengeAsync(Cadence.Weekly, lastMonday);
        await SeedCompletionAsync(challengeId, lastMonday);

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");

        response!.Open.Should().ContainSingle(i => i.Id == challengeId);
        response.DoneThisPeriod.Should().NotContain(i => i.Id == challengeId);
    }

    [Fact]
    public async Task Get_DoneThisPeriod_Sorted_By_SortOrder_Ascending()
    {
        var today = await CurrentPeriodStartAsync(Cadence.Daily);
        var higherSortOrder = await SeedChallengeAsync(Cadence.Daily, today, sortOrder: 5);
        await SeedCompletionAsync(higherSortOrder, today);
        var lowerSortOrder = await SeedChallengeAsync(Cadence.Daily, today, sortOrder: 1);
        await SeedCompletionAsync(lowerSortOrder, today);

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");

        response!.DoneThisPeriod.Select(i => i.Id).Should().Equal(lowerSortOrder, higherSortOrder);
    }

    [Fact]
    public async Task Get_Open_TieBreaks_By_SortOrder_When_Same_Cadence()
    {
        var currentMonday = await CurrentPeriodStartAsync(Cadence.Weekly);
        var higherSortOrder = await SeedChallengeAsync(Cadence.Weekly, currentMonday, sortOrder: 3);
        var lowerSortOrder = await SeedChallengeAsync(Cadence.Weekly, currentMonday, sortOrder: 0);

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");

        response!.Open.Select(i => i.Id).Should().Equal(lowerSortOrder, higherSortOrder);
    }

    // Daily's DaysRemaining is always the global minimum (0). Even on the rare day Weekly also
    // reads 0 (Sunday), the SortOrder tie-break resolves to the same order - holds unconditionally.
    [Fact]
    public async Task Get_Open_Sorts_Daily_Before_Other_Cadence_By_Urgency()
    {
        var today = await CurrentPeriodStartAsync(Cadence.Daily);
        var currentMonday = await CurrentPeriodStartAsync(Cadence.Weekly);
        var dailyId = await SeedChallengeAsync(Cadence.Daily, today, sortOrder: 0);
        var weeklyId = await SeedChallengeAsync(Cadence.Weekly, currentMonday, sortOrder: 1);

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");

        response!.Open.Select(i => i.Id).Should().Equal(dailyId, weeklyId);
    }

    [Fact]
    public async Task Get_Wires_Streak_Through_From_StreakQuery()
    {
        var today = await CurrentPeriodStartAsync(Cadence.Daily);
        var startsOn = today.AddDays(-10);
        var challengeId = await SeedChallengeAsync(Cadence.Daily, startsOn);
        await SeedCompletionAsync(challengeId, today);
        await SeedCompletionAsync(challengeId, today.AddDays(-1));

        await using var db = CreateDbContext();
        var expected = await new StreakQuery(db).ForChallenge(challengeId, Cadence.Daily, startsOn, null, today, CancellationToken.None);

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");
        var item = response!.DoneThisPeriod.Single(i => i.Id == challengeId);

        item.Streak.Length.Should().Be(expected.Length);
        item.Streak.IsAlive.Should().Be(expected.IsAlive);
        item.Streak.LastCompletedPeriod.Should().Be(expected.LastCompletedPeriod);
    }

    [Fact]
    public async Task Get_Streak_Reflects_Fresh_Challenge_With_No_Completions()
    {
        var today = await CurrentPeriodStartAsync(Cadence.Daily);
        var challengeId = await SeedChallengeAsync(Cadence.Daily, today);

        var response = await Client.GetFromJsonAsync<DashboardResponse>("/api/dashboard/");
        var item = response!.Open.Single(i => i.Id == challengeId);

        item.Streak.Length.Should().Be(0);
        item.Streak.IsAlive.Should().BeFalse();
        item.Streak.LastCompletedPeriod.Should().BeNull();
    }
}
