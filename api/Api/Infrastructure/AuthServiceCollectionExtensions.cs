using Api.Data;
using Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace Api.Infrastructure;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        // AddIdentityCore, not AddIdentity: no cookie scheme, no roles. See ADR 0009.
        services.AddIdentityCore<User>(options =>
            {
                // Explicit rather than left as a framework default - a security-relevant number
                // should be a deliberate choice. Starting values for a personal single-account app.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme, options =>
            {
                options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
            });
        // No RefreshTokenExpiration here: the built-in refresh path is not used at all. Refresh
        // tokens are hand-rolled (RefreshTokens table, ADR 0009) and carry their own ExpiresAt.

        services.AddAuthorization();

        return services;
    }
}
