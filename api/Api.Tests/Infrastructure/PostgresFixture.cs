using Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Api.Tests.Infrastructure;

// Independent of ASP.NET Core hosting - builds its own throwaway AppDbContext rather than
// going through ApiFactory's DI container, so it could stand alone for a non-HTTP harness.
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public async Task ResetDatabaseAsync()
    {
        await using var db = CreateDbContext();

        // Naming both tables in one TRUNCATE satisfies the Completion -> Challenge Restrict FK
        // without needing CASCADE. No RESTART IDENTITY: every PK is a Guid, no serial columns.
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Completions\", \"Challenges\";");

        var userCount = await db.Users.CountAsync();
        if (userCount != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly 1 seeded user after reset, found {userCount}. " +
                "A test likely created/deleted a User - fix the test, don't touch the reset logic.");
        }
    }

    private AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
}
