using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Data;
using Api.Features.Auth;
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
    // every test with no per-test boilerplate. Every protected endpoint now needs a real
    // bearer token, so this also logs Client in for real against the seeded dev user
    // (DevSeed.DefaultUsername/DefaultPassword - ApiFactory runs Development, so
    // DatabaseInitializer.ResolveSeedCredentials falls back to these) rather than bypassing
    // the login endpoint. The seeded Users row is shared across the whole test run, not reset
    // per test (see PostgresFixture) - a test that calls /logout or triggers reuse-detection
    // revokes this token's whole refresh-token family, so any test doing that must mint its own
    // separate token instead of relying on this one (already true by construction for
    // AuthEndpointsTests, which creates and tears down its own throwaway users).
    public async Task InitializeAsync()
    {
        await postgres.ResetDatabaseAsync();

        var loginResponse = await Client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(DevSeed.DefaultUsername, DevSeed.DefaultPassword));
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // Exposed so a test can trigger a second, mid-test reset to prove reset behavior directly,
    // instead of relying on cross-test execution order.
    protected Task ResetDatabase() => postgres.ResetDatabaseAsync();

    // Returns a new AppDbContext every call - use a separate instance for arrange and for
    // assert, never the same one, or a broken save can look correct via the change tracker
    // instead of the database.
    protected AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.ConnectionString).Options);

    // For tests asserting 401-without-token behavior, and for the auth endpoint tests
    // themselves - a fresh client with no Authorization header and an empty cookie jar,
    // unlike Client above.
    protected HttpClient CreateAnonymousClient() => Factory.CreateClient();
}
