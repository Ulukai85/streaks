using System.Security.Claims;

namespace Api.Infrastructure;

// Replaces DevCurrentUserProvider now that real auth is wired up (Phase 3 Stage 3).
public sealed class HttpCurrentUserProvider(IHttpContextAccessor httpContextAccessor) : ICurrentUserProvider
{
    // Only ever resolved inside a .RequireAuthorization() endpoint, so HttpContext.User is
    // guaranteed to carry this claim - it's put there by UserClaimsPrincipalFactory when the
    // access token was minted (AuthEndpoints.MintAccessTokenAsync).
    public Guid UserId => Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
