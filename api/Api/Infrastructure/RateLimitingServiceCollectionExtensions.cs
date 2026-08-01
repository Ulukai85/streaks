using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Infrastructure;

public static class RateLimitingServiceCollectionExtensions
{
    public const string LoginPolicy = "login";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global, not per-IP: the request path (host Caddy -> compose Caddy -> api) has two
            // proxy hops and nothing here trusts X-Forwarded-For yet, so RemoteIpAddress would
            // just be the compose Caddy's internal IP for every client. A shared cap still
            // meaningfully bounds a credential-stuffing burst; Identity's own per-account
            // lockout (see AddAppAuthentication) covers targeted guessing against one account.
            options.AddFixedWindowLimiter(LoginPolicy, limiterOptions =>
            {
                // IntegrationTestBase.InitializeAsync logs in through this exact endpoint for
                // every single test (ApiFactory runs Development) - a real per-minute cap here
                // would rate-limit the test suite itself, not an attacker. Only enforce it
                // outside Development.
                limiterOptions.PermitLimit = environment.IsDevelopment() ? int.MaxValue : 10;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });
        });

        return services;
    }
}
