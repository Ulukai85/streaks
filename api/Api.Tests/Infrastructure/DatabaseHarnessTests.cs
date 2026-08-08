using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

// Proves the harness itself works - not a feature test. Feature tests extend
// IntegrationTestBase directly instead of this class.
public class DatabaseHarnessTests(PostgresFixture postgres, ApiFactory factory) : IntegrationTestBase(postgres, factory)
{
    [Fact]
    public async Task DevUser_RoundTrips_Through_AppDbContext()
    {
        await using var db = CreateDbContext();

        var users = await db.Users.AsNoTracking().ToListAsync();

        users.Should().ContainSingle();
        users.Single().TimeZoneId.Should().Be("Europe/Berlin");
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Success_Through_Real_Pipeline()
    {
        var response = await Client.GetAsync("/api/health/");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Health_Endpoint_Accepts_Head_Requests()
    {
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/health/"));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ResetDatabaseAsync_RemovesChallenges()
    {
        await using var db = CreateDbContext();

        var userId = (await db.Users.AsNoTracking().SingleAsync()).Id;
        db.Challenges.Add(new Challenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Isolation check challenge",
            Cadence = Cadence.Daily,
            TargetCount = 1,
            StartsOn = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime),
            Color = "blue",
            SortOrder = 0,
        });
        await db.SaveChangesAsync();

        (await db.Challenges.CountAsync()).Should().Be(1);

        await ResetDatabase();

        (await db.Challenges.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Completion_UniqueConstraint_On_ChallengeId_And_PeriodStart_Is_Enforced()
    {
        await using var arrangeDb = CreateDbContext();

        var userId = (await arrangeDb.Users.AsNoTracking().SingleAsync()).Id;
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Unique constraint check challenge",
            Cadence = Cadence.Daily,
            TargetCount = 1,
            StartsOn = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime),
            Color = "blue",
            SortOrder = 0,
        };
        arrangeDb.Challenges.Add(challenge);
        await arrangeDb.SaveChangesAsync();

        var periodStart = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        await using var firstDb = CreateDbContext();
        firstDb.Completions.Add(new Completion
        {
            Id = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            PeriodStart = periodStart,
            CompletedAt = DateTimeOffset.UtcNow,
        });
        await firstDb.SaveChangesAsync();

        await using var secondDb = CreateDbContext();
        secondDb.Completions.Add(new Completion
        {
            Id = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            PeriodStart = periodStart,
            CompletedAt = DateTimeOffset.UtcNow,
        });

        var act = async () => await secondDb.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
