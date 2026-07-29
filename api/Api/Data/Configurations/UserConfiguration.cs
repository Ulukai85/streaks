using Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.TimeZoneId)
            .IsRequired()
            .HasMaxLength(64);

        // Identity's own columns, configured here rather than inherited from IdentityDbContext
        // (see ADR 0009). Lengths match the framework's defaults.
        builder.Property(u => u.UserName).HasMaxLength(256);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(256);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.NormalizedEmail).HasMaxLength(256);
        builder.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();

        // UserManager looks users up and enforces uniqueness by normalized name, and it trusts
        // the store to make that unique - without this index a duplicate username is a race
        // away. Postgres treats NULLs as distinct in a unique index, so rows that never got a
        // username don't collide.
        builder.HasIndex(u => u.NormalizedUserName)
            .IsUnique();
    }
}
