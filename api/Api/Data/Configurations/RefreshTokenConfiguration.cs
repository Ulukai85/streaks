using Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // Base64 of a SHA-256 hash is always 44 chars; 64 leaves room to switch encoding
        // without a migration.
        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // Rotation looks the presented token up by hash on every refresh; unique because two
        // rows sharing a hash would make "which row do I revoke?" ambiguous.
        builder.HasIndex(t => t.TokenHash)
            .IsUnique();

        // Reuse detection revokes a whole family at once: WHERE UserId = ? AND FamilyId = ?.
        builder.HasIndex(t => new { t.UserId, t.FamilyId });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
