using Api.Domain;

namespace Api.Data;

// Single hardcoded dev user for Phase 2 (no auth yet). Referenced by DatabaseInitializer's
// startup seed and by endpoints that need "the current user" until Phase 3 adds auth.
public static class DevSeed
{
    internal static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static User CreateUser() => new()
    {
        Id = UserId,
        TimeZoneId = "Europe/Berlin",
    };
}
