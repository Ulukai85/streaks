using System.Net;
using System.Net.Http.Json;
using Api.Domain;
using Api.Features.Auth;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Features.Auth;

// Every test here uses its own anonymous client (CreateAnonymousClient()), not the inherited
// Client - IntegrationTestBase.InitializeAsync now logs Client in for real against the shared
// seeded dev user, which would collide with the throwaway users/cookies these tests manage
// themselves (Client's own refresh cookie would otherwise satisfy e.g.
// Refresh_Returns_401_When_Cookie_Missing).
public class AuthEndpointsTests(PostgresFixture postgres, ApiFactory factory) : IntegrationTestBase(postgres, factory)
{
    [Fact]
    public async Task Login_Succeeds_Returns_AccessToken_And_HttpOnly_Cookie_No_RefreshToken_In_Body() =>
        await WithTestUserAsync(async (_, username, password) =>
        {
            var client = CreateAnonymousClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            body.Should().NotBeNull();
            body!.AccessToken.Should().NotBeNullOrWhiteSpace();

            var rawBody = await response.Content.ReadAsStringAsync();
            rawBody.Should().NotContain(
                "refreshToken", "the refresh token must never appear in the response body - see ADR 0008");

            response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
            var cookie = cookies!.Single(c => c.StartsWith("refresh_token="));
            var lowerCookie = cookie.ToLowerInvariant();
            lowerCookie.Should().Contain("httponly", "the cookie must be unreadable by JavaScript - see ADR 0008");
            lowerCookie.Should().Contain("path=/api/auth");
        });

    [Fact]
    public async Task Login_Returns_401_For_Wrong_Password() =>
        await WithTestUserAsync(async (_, username, _) =>
        {
            var client = CreateAnonymousClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "definitely-wrong"));

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem!.Detail.Should().Be("Invalid username or password.");
        });

    [Fact]
    public async Task Login_Returns_401_For_Unknown_Username_With_Same_Generic_Message()
    {
        var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest($"nobody-{Guid.NewGuid():N}", "whatever"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Be("Invalid username or password.");
    }

    [Fact]
    public async Task Refresh_Rotates_Token_And_Revokes_Previous() =>
        await WithTestUserAsync(async (userId, username, password) =>
        {
            var client = CreateAnonymousClient();
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));

            var refreshResponse = await client.PostAsync("/api/auth/refresh", null);

            refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
            body.Should().NotBeNull();
            body!.AccessToken.Should().NotBeNullOrWhiteSpace();

            await using var db = CreateDbContext();
            var tokens = await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
            tokens.Should().HaveCount(2);
            tokens.Should().ContainSingle(t => t.RevokedAt != null);
            tokens.Should().ContainSingle(t => t.RevokedAt == null);
            tokens.Select(t => t.FamilyId).Distinct().Should().ContainSingle();
        });

    [Fact]
    public async Task Refresh_Returns_401_When_Cookie_Missing()
    {
        var client = CreateAnonymousClient();
        var response = await client.PostAsync("/api/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_With_Already_Rotated_Token_Revokes_Whole_Family() =>
        await WithTestUserAsync(async (userId, username, password) =>
        {
            var client = CreateAnonymousClient();
            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
            var firstRawToken = ExtractRawCookieValue(loginResponse);

            // Rotate once via the client - its cookie jar now holds the second-generation token.
            var firstRefresh = await client.PostAsync("/api/auth/refresh", null);
            firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

            // Replay the original (now-revoked) token directly - this is the reuse/compromise
            // signal RefreshTokenIssuer.RotateAsync exists to catch. Uses yet another client with
            // its own cookie handling disabled: an auto-cookie client would silently overwrite
            // this manually-set header with its own (rotated) stored cookie otherwise.
            using var rawClient = Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            replayRequest.Headers.Add("Cookie", $"refresh_token={firstRawToken}");
            var replayResponse = await rawClient.SendAsync(replayRequest);
            replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // The whole family is now revoked - even the second-generation token still sitting
            // in the client's own cookie jar must be rejected too.
            var secondRefresh = await client.PostAsync("/api/auth/refresh", null);
            secondRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            await using var db = CreateDbContext();
            var tokens = await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
            tokens.Should().HaveCount(2);
            tokens.Should().OnlyContain(t => t.RevokedAt != null);
        });

    [Fact]
    public async Task Refresh_Returns_401_For_Expired_Token() =>
        await WithTestUserAsync(async (userId, username, password) =>
        {
            var client = CreateAnonymousClient();
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));

            await using (var db = CreateDbContext())
            {
                var token = await db.RefreshTokens.SingleAsync(t => t.UserId == userId);
                token.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
                await db.SaveChangesAsync();
            }

            var response = await client.PostAsync("/api/auth/refresh", null);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        });

    [Fact]
    public async Task Logout_Revokes_Token_Clears_Cookie_And_Following_Refresh_Fails() =>
        await WithTestUserAsync(async (userId, username, password) =>
        {
            var client = CreateAnonymousClient();
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));

            var logoutResponse = await client.PostAsync("/api/auth/logout", null);
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var refreshResponse = await client.PostAsync("/api/auth/refresh", null);
            refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            await using var db = CreateDbContext();
            var token = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == userId);
            token.RevokedAt.Should().NotBeNull();
        });

    [Fact]
    public async Task Logout_Is_Not_An_Error_When_No_Cookie_Present()
    {
        var client = CreateAnonymousClient();
        var response = await client.PostAsync("/api/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Protected_Endpoint_Requires_Authentication()
    {
        var anonymousResponse = await CreateAnonymousClient().GetAsync("/api/challenges/");
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Client is already authenticated against the seeded dev user - see
        // IntegrationTestBase.InitializeAsync.
        var authenticatedResponse = await Client.GetAsync("/api/challenges/");
        authenticatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Wraps the create/run/cleanup lifecycle shared by every test above that needs a throwaway
    // user, replacing a repeated try/finally around CreateTestUserAsync()/DeleteTestUserAsync().
    private async Task WithTestUserAsync(Func<Guid, string, string, Task> body)
    {
        (Guid userId, string username, string password) = await CreateTestUserAsync();
        try
        {
            await body(userId, username, password);
        }
        finally
        {
            await DeleteTestUserAsync(userId);
        }
    }

    private async Task<(Guid UserId, string Username, string Password)> CreateTestUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var username = $"test-{Guid.NewGuid():N}";
        const string password = "Correct-Horse-Battery-1!";

        var user = new User { Id = Guid.NewGuid(), UserName = username, TimeZoneId = "Europe/Berlin" };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue();

        return (user.Id, username, password);
    }

    // Reset only truncates Completions/Challenges and asserts exactly 1 User remains (see
    // PostgresFixture) - every test that adds a user via CreateTestUserAsync must remove it,
    // RefreshTokens first (Restrict FK), or the next test's reset assertion fails.
    private async Task DeleteTestUserAsync(Guid userId)
    {
        await using var db = CreateDbContext();
        await db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
    }

    private static string ExtractRawCookieValue(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookie = cookies!.Single(c => c.StartsWith("refresh_token="));
        var value = cookie[(cookie.IndexOf('=') + 1)..];
        return value[..value.IndexOf(';')];
    }
}
