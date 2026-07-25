using Api.Data;

namespace Api.Infrastructure;

// Single hardcoded dev user for Phase 2 (no auth yet) - see DevSeed. Phase 3 replaces this
// with a real implementation reading the authenticated user's id from HttpContext.
public sealed class DevCurrentUserProvider : ICurrentUserProvider
{
    public Guid UserId => DevSeed.UserId;
}
