using System.Net;
using System.Net.Http.Json;
using Api.Domain;
using Api.Features.Challenges;
using Api.Features.Completions;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Features.Completions;

public class CompletionEndpointsTests(PostgresFixture postgres, ApiFactory factory) : IntegrationTestBase(postgres, factory)
{
    private async Task<Guid> CreateChallengeAsync(string cadence = "Daily")
    {
        var response = await Client.PostAsJsonAsync("/api/challenges/", new CreateChallengeRequest($"Challenge {Guid.NewGuid()}", null, cadence, "blue", null));
        var body = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        return body!.Id;
    }

    private async Task<DateOnly> CurrentPeriodStartAsync(Cadence cadence)
    {
        await using var db = CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync();
        return PeriodCalculator.PeriodStartFor(DateTimeOffset.UtcNow, user.TimeZoneId, cadence);
    }

    // StartsOn isn't client-settable (Stage 4) - creating via HTTP always sets it to "now", which
    // would collide with the retroactive-bound tests below. Backdating it directly isolates the
    // periodsAgo bound check from the separate "PeriodStart before StartsOn" check.
    private async Task BackdateStartsOnAsync(Guid challengeId, DateOnly startsOn)
    {
        await using var db = CreateDbContext();
        var challenge = await db.Challenges.SingleAsync(c => c.Id == challengeId);
        challenge.StartsOn = startsOn;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Post_Creates_Completion_For_Current_Period_When_PeriodStart_Omitted()
    {
        var challengeId = await CreateChallengeAsync();
        var currentPeriodStart = await CurrentPeriodStartAsync(Cadence.Daily);

        var response = await Client.PostAsJsonAsync($"/api/challenges/{challengeId}/completions", new CompleteChallengeRequest(null, "Felt good"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<CompletionResponse>();
        body.Should().NotBeNull();
        body!.ChallengeId.Should().Be(challengeId);
        body.PeriodStart.Should().Be(currentPeriodStart);
        body.Note.Should().Be("Felt good");

        await using var db = CreateDbContext();
        var persisted = await db.Completions.AsNoTracking().SingleAsync(c => c.Id == body.Id);
        persisted.PeriodStart.Should().Be(currentPeriodStart);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Post_Accepts_PeriodStart_Within_Bound(int periodsAgo)
    {
        var challengeId = await CreateChallengeAsync();
        var currentPeriodStart = await CurrentPeriodStartAsync(Cadence.Daily);
        await BackdateStartsOnAsync(challengeId, currentPeriodStart.AddDays(-10));
        var periodStart = currentPeriodStart.AddDays(-periodsAgo);

        var response = await Client.PostAsJsonAsync($"/api/challenges/{challengeId}/completions", new CompleteChallengeRequest(periodStart, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_Rejects_PeriodStart_MoreThanTwoPeriodsAgo()
    {
        var challengeId = await CreateChallengeAsync();
        var currentPeriodStart = await CurrentPeriodStartAsync(Cadence.Daily);

        var response = await Client.PostAsJsonAsync(
            $"/api/challenges/{challengeId}/completions",
            new CompleteChallengeRequest(currentPeriodStart.AddDays(-3), null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Rejects_PeriodStart_Before_Challenge_StartsOn()
    {
        var challengeId = await CreateChallengeAsync();
        var currentPeriodStart = await CurrentPeriodStartAsync(Cadence.Daily);

        // Challenge.StartsOn is set to currentPeriodStart at creation time (Stage 4). A period
        // one day earlier is still within the retroactive bound (periodsAgo == 1) but predates
        // the challenge's existence, so it must be rejected on that separate ground.
        var response = await Client.PostAsJsonAsync(
            $"/api/challenges/{challengeId}/completions",
            new CompleteChallengeRequest(currentPeriodStart.AddDays(-1), null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Rejects_Future_PeriodStart()
    {
        var challengeId = await CreateChallengeAsync();
        var currentPeriodStart = await CurrentPeriodStartAsync(Cadence.Daily);

        var response = await Client.PostAsJsonAsync(
            $"/api/challenges/{challengeId}/completions",
            new CompleteChallengeRequest(currentPeriodStart.AddDays(1), null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Rejects_Misaligned_PeriodStart_For_Weekly_Challenge()
    {
        var challengeId = await CreateChallengeAsync("Weekly");
        var currentMonday = await CurrentPeriodStartAsync(Cadence.Weekly);

        var response = await Client.PostAsJsonAsync(
            $"/api/challenges/{challengeId}/completions",
            new CompleteChallengeRequest(currentMonday.AddDays(1), null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Returns_Conflict_For_Duplicate_Completion()
    {
        var challengeId = await CreateChallengeAsync();

        var first = await Client.PostAsJsonAsync($"/api/challenges/{challengeId}/completions", new CompleteChallengeRequest(null, null));
        var second = await Client.PostAsJsonAsync($"/api/challenges/{challengeId}/completions", new CompleteChallengeRequest(null, null));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_Returns_Conflict_For_Archived_Challenge()
    {
        var challengeId = await CreateChallengeAsync();
        await Client.PostAsync($"/api/challenges/{challengeId}/archive", null);

        var response = await Client.PostAsJsonAsync($"/api/challenges/{challengeId}/completions", new CompleteChallengeRequest(null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_Returns_NotFound_For_Unknown_ChallengeId()
    {
        var response = await Client.PostAsJsonAsync($"/api/challenges/{Guid.NewGuid()}/completions", new CompleteChallengeRequest(null, null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_Persists_Null_Note_When_Blank()
    {
        var challengeId = await CreateChallengeAsync();

        var response = await Client.PostAsJsonAsync($"/api/challenges/{challengeId}/completions", new CompleteChallengeRequest(null, "   "));

        var body = await response.Content.ReadFromJsonAsync<CompletionResponse>();
        body!.Note.Should().BeNull();
    }
}
