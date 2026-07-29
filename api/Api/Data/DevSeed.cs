using Api.Domain;

namespace Api.Data;

// Fallback credentials used only when SEED_USER_NAME/SEED_USER_PASSWORD aren't configured and
// the host is running in Development - see DatabaseInitializer.ResolveSeedCredentials and
// decision #12 in docs/phase-3-plan.md. A Production boot with no SEED_USER_PASSWORD throws
// instead of silently seeding these - they are a dev/test convenience, not a secret.
public static class DevSeed
{
    internal static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public const string DefaultUsername = "dev";
    public const string DefaultPassword = "Dev-Password-123!";

    public static User CreateUser(string username) => new()
    {
        Id = UserId,
        UserName = username,
        TimeZoneId = "Europe/Berlin",
    };
}
