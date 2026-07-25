namespace Api.Tests.Infrastructure;

// Shares one Postgres container across every test class in the collection - container
// startup is the expensive part, TRUNCATE-based reset is cheap. ApiFactory is a per-test-class
// IClassFixture instead (xUnit v2 doesn't support one collection fixture depending on another
// via constructor injection, only class fixtures depending on collection fixtures) - rebuilding
// the WebApplicationFactory host per class is cheap next to container startup.
// Reference Name by symbol (never a string literal) in [Collection(...)] - a typo'd literal
// compiles fine but silently spins up a second, unshared container instead of failing loudly.
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Integration";
}
