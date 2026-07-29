using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Auth;

// Hand-rolled rather than Identity's internal RefreshTokenProtector: this buys per-device
// logout (revoke one row, not every session) and reuse/replay detection (a rotated-away
// token being presented again revokes its whole family). See ADR 0009, decision #9 in
// docs/phase-3-plan.md.
public class RefreshTokenIssuer(AppDbContext db, TimeProvider timeProvider)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);

    public async Task<(RefreshToken Token, string RawToken)> IssueAsync(Guid userId, CancellationToken ct)
    {
        (string rawToken, string hash) = GenerateToken();
        var now = timeProvider.GetUtcNow();

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = now + TokenLifetime,
            CreatedAt = now,
        };
        token.FamilyId = token.Id;

        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);

        return (token, rawToken);
    }

    public async Task<(RefreshToken Token, string RawToken)?> RotateAsync(string presentedRawToken, CancellationToken ct)
    {
        // Tracked query, deliberately not AsNoTracking(): existing.RevokedAt may be mutated and
        // saved below, unlike every other read path in this class.
        var hash = Hash(presentedRawToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        if (existing.RevokedAt is not null)
        {
            // The presented token was already rotated away - it can only show up again if the
            // cookie leaked. Kill every still-valid token in the chain, not just this one.
            await db.RefreshTokens
                .Where(t => t.FamilyId == existing.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), ct);
            return null;
        }

        if (existing.ExpiresAt <= now)
        {
            return null;
        }

        existing.RevokedAt = now;

        (string rawToken, string newHash) = GenerateToken();
        var next = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            FamilyId = existing.FamilyId,
            TokenHash = newHash,
            ExpiresAt = now + TokenLifetime,
            CreatedAt = now,
        };

        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(ct);

        return (next, rawToken);
    }

    public async Task RevokeAsync(string presentedRawToken, CancellationToken ct)
    {
        var hash = Hash(presentedRawToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, timeProvider.GetUtcNow()), ct);
    }

    private static (string RawToken, string Hash) GenerateToken()
    {
        var rawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (rawToken, Hash(rawToken));
    }

    private static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
