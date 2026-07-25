using Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
{
    public void Configure(EntityTypeBuilder<Challenge> builder)
    {
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Url)
            .HasMaxLength(2048);

        builder.Property(c => c.Cadence)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        // Pinned explicitly so a future Npgsql/EF version can't silently drift this off "date".
        builder.Property(c => c.StartsOn)
            .HasColumnType("date");

        builder.Property(c => c.Color)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.SortOrder)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Leading UserId (equality predicate) before ArchivedAt (filter) matches the
        // dashboard's only access pattern: WHERE UserId = ? AND ArchivedAt IS NULL.
        builder.HasIndex(c => new { c.UserId, c.ArchivedAt });
    }
}
