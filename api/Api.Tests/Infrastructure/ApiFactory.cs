using Api.Data;
using Microsoft.AspNetCore.Hosting;
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
        // Development so Program.cs's IsDevelopment()-gated dev-user seed fires for real
        // against the container when the host boots.
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // RemoveAll first: otherwise Program.cs's own registration (pointed at the real
            // dev DB from appsettings.Development.json) stays present alongside this one, and
            // registration order decides which wins.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgres.ConnectionString));
        });
    }
}
