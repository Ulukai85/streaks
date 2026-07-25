using Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class CompletionConfiguration : IEntityTypeConfiguration<Completion>
{
    public void Configure(EntityTypeBuilder<Completion> builder)
    {
        // Every period/streak comparison hinges on this being a date, never a timestamp -
        // pinned explicitly rather than trusting the provider default. See ADR 0001.
        builder.Property(c => c.PeriodStart)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.CompletedAt)
            .IsRequired();

        builder.Property(c => c.Note)
            .HasMaxLength(1000);

        // DB-enforced per ADR 0003 - test the constraint, not the code that avoids it.
        builder.HasIndex(c => new { c.ChallengeId, c.PeriodStart })
            .IsUnique();

        builder.HasOne<Challenge>()
            .WithMany()
            .HasForeignKey(c => c.ChallengeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
