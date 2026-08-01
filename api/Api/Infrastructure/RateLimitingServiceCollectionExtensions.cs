using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Infrastructure;

public static class RateLimitingServiceCollectionExtensions
{
    public const string LoginPolicy = "login";
    public const string RefreshPolicy = "refresh";

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

            // /refresh is unauthenticated-reachable the same way /login is (see AuthEndpoints)
            // and does a DB lookup per call (RefreshTokenIssuer.RotateAsync) even for a garbage
            // cookie - a flood with no valid session costs nothing to send but still hits the
            // database every time. Same reasoning as LoginPolicy above; a higher limit than
            // login's because a legitimate client calls this on every hard reload/new tab
            // (ADR 0008), not just on an explicit user action.
            options.AddFixedWindowLimiter(RefreshPolicy, limiterOptions =>
            {
                limiterOptions.PermitLimit = environment.IsDevelopment() ? int.MaxValue : 20;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });
        });

        return services;
    }
}
