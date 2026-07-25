using Microsoft.AspNetCore.Identity;

namespace Api.Domain;

// Derives IdentityUser<Guid> ahead of Phase 3's real Identity wiring so the FK shape
// (Challenge/Completion -> UserId) never has to change. See ADR 0007.
public class User : IdentityUser<Guid>
{
    public required string TimeZoneId { get; set; }
}
