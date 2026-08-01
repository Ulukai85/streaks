using Microsoft.AspNetCore.DataProtection;

namespace Api.Infrastructure;

public static class DataProtectionServiceCollectionExtensions
{
    public static IServiceCollection AddAppDataProtection(
        this IServiceCollection services, IHostEnvironment environment)
    {
        var builder = services.AddDataProtection().SetApplicationName("Streaks");

        // Outside Development, pin keys to the mounted volume so a container
        // redeploy doesn't silently invalidate every issued access token.
        // In Development there's no volume and no need for this.
        if (!environment.IsDevelopment())
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo("/keys"));
        }

        return services;
    }
}
