using Api.Domain;
using Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Api.Features.Auth;

public static class AuthEndpoints
{
    // Path-scoped so the cookie never rides along on ordinary /api/challenges calls - only
    // these three endpoints ever see it. See ADR 0008.
    private const string RefreshCookieName = "refresh_token";
    private const string RefreshCookiePath = "/api/auth";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Not .RequireAuthorization() on the group: login/refresh must be reachable
        // unauthenticated, and logout only needs the refresh cookie, not a bearer header.
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> (
            LoginRequest request,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            RefreshTokenIssuer refreshTokenIssuer,
            IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
            TimeProvider timeProvider,
            IHostEnvironment env,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var user = await userManager.FindByNameAsync(request.Username);
            if (user is null)
            {
                return InvalidCredentials();
            }

            var checkResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!checkResult.Succeeded)
            {
                return InvalidCredentials();
            }

            (string accessToken, DateTimeOffset expiresAt) = await MintAccessTokenAsync(signInManager, bearerTokenOptions, timeProvider, user);
            (RefreshToken refreshToken, string rawRefreshToken) = await refreshTokenIssuer.IssueAsync(user.Id, ct);

            SetRefreshCookie(httpContext, rawRefreshToken, refreshToken.ExpiresAt, env);

            return TypedResults.Ok(new LoginResponse(accessToken, expiresAt));
        }).AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.LoginPolicy);

        group.MapPost("/refresh", async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> (
            HttpContext httpContext,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            RefreshTokenIssuer refreshTokenIssuer,
            IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
            TimeProvider timeProvider,
            IHostEnvironment env,
            CancellationToken ct) =>
        {
            var presented = httpContext.Request.Cookies[RefreshCookieName];
            if (string.IsNullOrEmpty(presented))
            {
                return SessionInvalid();
            }

            var rotated = await refreshTokenIssuer.RotateAsync(presented, ct);
            if (rotated is null)
            {
                return SessionInvalid();
            }

            (RefreshToken refreshToken, string rawRefreshToken) = rotated.Value;

            // RefreshTokens.UserId has a Restrict FK to Users - this is guaranteed to resolve.
            var user = (await userManager.FindByIdAsync(refreshToken.UserId.ToString()))!;

            (string accessToken, DateTimeOffset expiresAt) = await MintAccessTokenAsync(signInManager, bearerTokenOptions, timeProvider, user);

            SetRefreshCookie(httpContext, rawRefreshToken, refreshToken.ExpiresAt, env);

            return TypedResults.Ok(new LoginResponse(accessToken, expiresAt));
        }).RequireRateLimiting(RateLimitingServiceCollectionExtensions.RefreshPolicy);

        group.MapPost("/logout", async Task<NoContent> (
            HttpContext httpContext,
            RefreshTokenIssuer refreshTokenIssuer,
            CancellationToken ct) =>
        {
            var presented = httpContext.Request.Cookies[RefreshCookieName];
            if (!string.IsNullOrEmpty(presented))
            {
                await refreshTokenIssuer.RevokeAsync(presented, ct);
            }

            httpContext.Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });

            return TypedResults.NoContent();
        });
    }

    private static async Task<(string AccessToken, DateTimeOffset ExpiresAt)> MintAccessTokenAsync(
        SignInManager<User> signInManager,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider,
        User user)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var options = bearerTokenOptions.Get(IdentityConstants.BearerScheme);
        var expiresAt = timeProvider.GetUtcNow() + options.BearerTokenExpiration;

        // Mirrors BearerTokenHandler.CreateBearerTicket/HandleSignInAsync exactly (source:
        // dotnet/aspnetcore, src/Security/Authentication/BearerToken/src/BearerTokenHandler.cs)
        // so the framework's own BearerTokenHandler.HandleAuthenticateAsync can unprotect and
        // validate what we mint here. We go through the protector directly instead of
        // Context.SignInAsync so the response body stays our own LoginResponse shape - no
        // refresh token in it, per ADR 0008 - instead of the framework's AccessTokenResponse.
        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties { ExpiresUtc = expiresAt },
            $"{IdentityConstants.BearerScheme}:AccessToken");

        return (options.BearerTokenProtector.Protect(ticket), expiresAt);
    }

    private static void SetRefreshCookie(HttpContext httpContext, string rawRefreshToken, DateTimeOffset expiresAt, IHostEnvironment env)
    {
        httpContext.Response.Cookies.Append(RefreshCookieName, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            // Secure requires HTTPS; Development runs over plain local HTTP. See ADR 0008.
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = expiresAt,
        });
    }

    private static Results<Ok<LoginResponse>, ProblemHttpResult> InvalidCredentials() =>
        TypedResults.Problem(detail: "Invalid username or password.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");

    private static Results<Ok<LoginResponse>, ProblemHttpResult> SessionInvalid() =>
        TypedResults.Problem(detail: "Session expired or invalid.", statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
}
