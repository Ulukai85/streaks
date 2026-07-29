using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

// [Collection] and IClassFixture<ApiFactory> here are both inherited by every derived test
// class - do not repeat either on subclasses.
[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase(PostgresFixture postgres, ApiFactory factory)
    : IClassFixture<ApiFactory>, IAsyncLifetime
{
    protected HttpClient Client { get; } = factory.CreateClient();

    // Exposed so a test can resolve a scoped service (e.g. UserManager<User>) directly
    // against the same DI container the HTTP pipeline runs against.
    protected ApiFactory Factory { get; } = factory;

    // xUnit constructs a fresh test-class instance per [Fact], so this resets the DB before
    // every test with no per-test boilerplate.
    public Task InitializeAsync() => postgres.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Exposed so a test can trigger a second, mid-test reset to prove reset behavior directly,
    // instead of relying on cross-test execution order.
    protected Task ResetDatabase() => postgres.ResetDatabaseAsync();

    // Returns a new AppDbContext every call - use a separate instance for arrange and for
    // assert, never the same one, or a broken save can look correct via the change tracker
    // instead of the database.
    protected AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.ConnectionString).Options);
}
