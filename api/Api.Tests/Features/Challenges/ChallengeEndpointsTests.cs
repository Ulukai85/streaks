using System.Net;
using System.Net.Http.Json;
using Api.Domain;
using Api.Features.Challenges;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Features.Challenges;

public class ChallengeEndpointsTests(PostgresFixture postgres, ApiFactory factory) : IntegrationTestBase(postgres, factory)
{
    [Fact]
    public async Task Post_Creates_Challenge_And_Persists()
    {
        await using var db = CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync();

        var request = new CreateChallengeRequest("Read every day", null, "Daily", "blue", null);

        var response = await Client.PostAsJsonAsync("/api/challenges/", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        body.Should().NotBeNull();
        body!.TargetCount.Should().Be(1);
        response.Headers.Location!.ToString().Should().Be($"/api/challenges/{body.Id}");

        var expectedStartsOn = PeriodCalculator.PeriodStartFor(DateTimeOffset.UtcNow, user.TimeZoneId, Cadence.Daily);
        body.StartsOn.Should().Be(expectedStartsOn);

        await using var assertDb = CreateDbContext();
        var persisted = await assertDb.Challenges.AsNoTracking().SingleAsync(c => c.Id == body.Id);
        persisted.Name.Should().Be("Read every day");
        persisted.Color.Should().Be("blue");
    }

    [Fact]
    public async Task Post_Computes_StartsOn_Using_Weekly_Cadence()
    {
        await using var db = CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync();

        var request = new CreateChallengeRequest("Weekly run", null, "Weekly", "blue", null);

        var response = await Client.PostAsJsonAsync("/api/challenges/", request);

        var body = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        body.Should().NotBeNull();

        var expectedStartsOn = PeriodCalculator.PeriodStartFor(DateTimeOffset.UtcNow, user.TimeZoneId, Cadence.Weekly);
        body!.StartsOn.Should().Be(expectedStartsOn);
    }

    [Fact]
    public async Task Post_Computes_StartsOn_Using_Monthly_Cadence()
    {
        await using var db = CreateDbContext();
        var user = await db.Users.AsNoTracking().SingleAsync();

        var request = new CreateChallengeRequest("Monthly chore", null, "Monthly", "blue", null);

        var response = await Client.PostAsJsonAsync("/api/challenges/", request);

        var body = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        body.Should().NotBeNull();

        var expectedStartsOn = PeriodCalculator.PeriodStartFor(DateTimeOffset.UtcNow, user.TimeZoneId, Cadence.Monthly);
        body!.StartsOn.Should().Be(expectedStartsOn);
    }

    [Fact]
    public async Task Post_Appends_SortOrder_When_Omitted()
    {
        var first = await Client.PostAsJsonAsync("/api/challenges/", new CreateChallengeRequest("First", null, "Daily", "blue", null));
        var firstBody = await first.Content.ReadFromJsonAsync<ChallengeResponse>();
        firstBody!.SortOrder.Should().Be(0);

        var second = await Client.PostAsJsonAsync("/api/challenges/", new CreateChallengeRequest("Second", null, "Daily", "green", null));
        var secondBody = await second.Content.ReadFromJsonAsync<ChallengeResponse>();
        secondBody!.SortOrder.Should().Be(firstBody.SortOrder + 1);
    }

    [Fact]
    public async Task Post_Returns_ValidationProblem_For_Empty_Name()
    {
        var request = new CreateChallengeRequest("", null, "Daily", "blue", null);

        var response = await Client.PostAsJsonAsync("/api/challenges/", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().ContainKey("Name");
    }

    [Fact]
    public async Task Get_Returns_NonArchived_Challenges_Ordered_By_SortOrder()
    {
        await using var db = CreateDbContext();
        var userId = (await db.Users.AsNoTracking().SingleAsync()).Id;

        Challenge NewChallenge(string name, int sortOrder, bool archived) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Cadence = Cadence.Daily,
            TargetCount = 1,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Color = "blue",
            SortOrder = sortOrder,
            ArchivedAt = archived ? DateTimeOffset.UtcNow : null,
        };

        db.Challenges.AddRange(
            NewChallenge("Second", 2, archived: false),
            NewChallenge("Archived", 1, archived: true),
            NewChallenge("First", 0, archived: false));
        await db.SaveChangesAsync();

        var response = await Client.GetFromJsonAsync<List<ChallengeResponse>>("/api/challenges/");

        response.Should().NotBeNull();
        response!.Select(c => c.Name).Should().Equal("First", "Second");
    }

    [Fact]
    public async Task Post_Archive_Sets_ArchivedAt()
    {
        var created = await Client.PostAsJsonAsync("/api/challenges/", new CreateChallengeRequest("Archive me", null, "Daily", "blue", null));
        var createdBody = await created.Content.ReadFromJsonAsync<ChallengeResponse>();

        var response = await Client.PostAsync($"/api/challenges/{createdBody!.Id}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChallengeResponse>();
        body!.ArchivedAt.Should().NotBeNull();

        await using var db = CreateDbContext();
        var persisted = await db.Challenges.AsNoTracking().SingleAsync(c => c.Id == createdBody.Id);
        persisted.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_Archive_Is_Idempotent()
    {
        var created = await Client.PostAsJsonAsync("/api/challenges/", new CreateChallengeRequest("Archive twice", null, "Daily", "blue", null));
        var createdBody = await created.Content.ReadFromJsonAsync<ChallengeResponse>();

        var firstArchive = await Client.PostAsync($"/api/challenges/{createdBody!.Id}/archive", null);
        var firstBody = await firstArchive.Content.ReadFromJsonAsync<ChallengeResponse>();

        var secondArchive = await Client.PostAsync($"/api/challenges/{createdBody.Id}/archive", null);
        var secondBody = await secondArchive.Content.ReadFromJsonAsync<ChallengeResponse>();

        firstArchive.StatusCode.Should().Be(HttpStatusCode.OK);
        secondArchive.StatusCode.Should().Be(HttpStatusCode.OK);

        // BeCloseTo, not Be: the first response is the unrounded in-memory value, the second
        // is read back from Postgres, whose timestamptz truncates to microsecond precision
        // (.NET DateTimeOffset ticks are 100ns) - a real precision-loss gap, not test flakiness.
        secondBody!.ArchivedAt.Should().BeCloseTo(firstBody!.ArchivedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Post_Archive_Returns_404_For_Unknown_Id()
    {
        var response = await Client.PostAsync($"/api/challenges/{Guid.NewGuid()}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
