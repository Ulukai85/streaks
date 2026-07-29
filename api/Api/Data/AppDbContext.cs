using Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<Completion> Completions => Set<Completion>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ConfigureIdentityStores(modelBuilder);
    }

    // Deliberately not IdentityDbContext/IdentityUserContext - see ADR 0009. These three
    // entities are framework plumbing rather than domain entities, so they are configured
    // here as a set instead of getting IEntityTypeConfiguration<T> files of their own
    // alongside Challenge/Completion/User.
    //
    // AddIdentityCore<User>() is called without .AddRoles<TRole>(), so the EF store resolves
    // to UserOnlyStore and only these three are needed - no Roles/UserRoles/RoleClaims.
    // Nothing in the login/refresh/logout flow reads or writes them; they exist because
    // UserOnlyStore's constructor requires them to be part of the model. They should stay
    // empty in practice.
    private static void ConfigureIdentityStores(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityUserClaim<Guid>>(b =>
        {
            b.ToTable("UserClaims");
            b.HasKey(c => c.Id);

            // Cascade rather than this project's usual Restrict default: unlike a Challenge,
            // these rows carry no meaning without their user, and UserManager.DeleteAsync
            // expects the store to clean them up.
            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(b =>
        {
            b.ToTable("UserLogins");
            b.HasKey(l => new { l.LoginProvider, l.ProviderKey });
            b.Property(l => l.LoginProvider).HasMaxLength(128);
            b.Property(l => l.ProviderKey).HasMaxLength(128);

            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(b =>
        {
            b.ToTable("UserTokens");
            b.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
            b.Property(t => t.LoginProvider).HasMaxLength(128);
            b.Property(t => t.Name).HasMaxLength(128);

            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
