namespace Api.Domain;

// Hand-rolled refresh-token store rather than Identity's internal RefreshTokenProtector,
// so that logout can revoke one device without ending every session (one row) and a
// replayed token can revoke its whole rotation chain (one FamilyId). See ADR 0009.
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Equals Id on first issuance and is carried forward unchanged on every rotation.
    // Presenting an already-revoked token means the chain leaked: revoke the whole family.
    public Guid FamilyId { get; set; }

    // SHA-256 of the raw token, never the raw token itself - a database dump must not
    // hand out usable credentials.
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
