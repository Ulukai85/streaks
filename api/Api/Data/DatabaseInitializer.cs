using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public static class DatabaseInitializer
{
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (app.Environment.IsDevelopment() && !await db.Users.AnyAsync())
        {
            db.Users.Add(DevSeed.CreateUser());
            await db.SaveChangesAsync();
        }
    }
}
