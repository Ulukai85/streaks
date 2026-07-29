using Api.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Api.Tests.Data;

// Pure unit tests, deliberately not integration tests: the fail-fast rule this covers
// (decision #12 in docs/phase-3-plan.md) only fires when Users is empty, and the shared
// PostgresFixture/ApiFactory always has exactly one seeded user by the time any integration
// test runs - see PostgresFixture.ResetDatabaseAsync's invariant. Testing
// ResolveSeedCredentials directly sidesteps that entirely.
public class DatabaseInitializerTests
{
    [Fact]
    public void ResolveSeedCredentials_Throws_When_Production_Missing_SeedPassword()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment(Environments.Production);

        var act = () => DatabaseInitializer.ResolveSeedCredentials(configuration, environment);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SEED_USER_PASSWORD*");
    }

    [Fact]
    public void ResolveSeedCredentials_Falls_Back_To_Dev_Defaults_In_Development()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment(Environments.Development);

        (string username, string password) = DatabaseInitializer.ResolveSeedCredentials(configuration, environment);

        username.Should().Be(DevSeed.DefaultUsername);
        password.Should().Be(DevSeed.DefaultPassword);
    }

    [Fact]
    public void ResolveSeedCredentials_Uses_Configured_Values_When_Present_Even_Outside_Development()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SEED_USER_NAME"] = "admin",
                ["SEED_USER_PASSWORD"] = "S3cure-Pass!",
            })
            .Build();
        var environment = new FakeHostEnvironment(Environments.Production);

        (string username, string password) = DatabaseInitializer.ResolveSeedCredentials(configuration, environment);

        username.Should().Be("admin");
        password.Should().Be("S3cure-Pass!");
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = nameof(DatabaseInitializerTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
