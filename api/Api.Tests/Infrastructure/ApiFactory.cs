using Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests.Infrastructure;

public class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development, not Testing: DatabaseInitializer.ResolveSeedCredentials only falls back
        // to DevSeed's hardcoded username/password under IsDevelopment() (decision #12), and
        // AuthEndpoints' refresh cookie is only Secure=false under IsDevelopment() - both are
        // needed for the plain-HTTP TestServer client to log in and carry the cookie at all.
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // RemoveAll first: otherwise Program.cs's own registration (pointed at the real
            // dev DB from appsettings.Development.json) stays present alongside this one, and
            // registration order decides which wins.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgres.ConnectionString));

            // Full-strength PBKDF2 is expensive by design; IntegrationTestBase now performs a
            // real /login once per test. Test host only - Production hashing is untouched.
            services.Configure<PasswordHasherOptions>(options => options.IterationCount = 1);
        });
    }
}
