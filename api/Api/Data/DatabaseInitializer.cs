using Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public static class DatabaseInitializer
{
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync())
        {
            return;
        }

        (string username, string password) = ResolveSeedCredentials(app.Configuration, app.Environment);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var result = await userManager.CreateAsync(DevSeed.CreateUser(username), password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed the initial user: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    // Pulled out of MigrateAndSeedAsync so the fail-fast rule can be unit tested without a
    // database or a full host - see DatabaseInitializerTests.
    public static (string Username, string Password) ResolveSeedCredentials(IConfiguration configuration, IHostEnvironment environment)
    {
        var username = configuration["SEED_USER_NAME"] ?? DevSeed.DefaultUsername;
        var password = configuration["SEED_USER_PASSWORD"];

        if (!string.IsNullOrEmpty(password))
        {
            return (username, password);
        }

        if (environment.IsDevelopment())
        {
            return (username, DevSeed.DefaultPassword);
        }

        // Per decision #12: never silently fall back to the hardcoded dev password outside
        // Development - a fresh Production deploy must refuse to boot instead.
        throw new InvalidOperationException(
            "SEED_USER_PASSWORD must be set outside Development - refusing to boot with the hardcoded dev password on a real deploy.");
    }
}
