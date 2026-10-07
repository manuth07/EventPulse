using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EventPulse.IdentityService.Models;

namespace EventPulse.IdentityService.Data;

/// <summary>
/// EventPulse Identity DbContext.
/// Owns all ASP.NET Core Identity tables and the EmailVerificationCode entity.
/// Connects exclusively to: eventpulse_identity.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<OrganizerApplication> OrganizerApplications => Set<OrganizerApplication>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // -----------------------------------------------------------------------
        // ApplicationUser
        // -----------------------------------------------------------------------
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.CountryCode)
                .HasMaxLength(2);

            entity.Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // ASP.NET Identity already creates a unique index on NormalizedEmail.
            // One EventPulse account per normalised email is enforced by Identity.
        });

        // -----------------------------------------------------------------------
        // EmailVerificationCode
        // -----------------------------------------------------------------------
        builder.Entity<EmailVerificationCode>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CodeHash)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.User)
                .WithMany(u => u.EmailVerificationCodes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index to quickly find all codes for a given user
            entity.HasIndex(e => e.UserId);

            // Index to support active-code lookup by user + expiry
            entity.HasIndex(e => new { e.UserId, e.ExpiresAt });
        });

        // -----------------------------------------------------------------------
        // PasswordResetToken
        // -----------------------------------------------------------------------
        builder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.TokenHash)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(t => t.ExpiresAt)
                .IsRequired();

            entity.Property(t => t.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(t => t.UsedAt)
                .IsConcurrencyToken();

            entity.HasOne(t => t.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index to quickly find all reset tokens for a given user
            entity.HasIndex(t => t.UserId);

            // Index to look up a token by hash
            entity.HasIndex(t => t.TokenHash);

            // Composite index to support active token lookup by user + expiry
            entity.HasIndex(t => new { t.UserId, t.ExpiresAt });
        });

        // -----------------------------------------------------------------------
        // OrganizerApplication
        // -----------------------------------------------------------------------
        builder.Entity<OrganizerApplication>(entity =>
        {
            entity.ToTable("OrganizerApplications");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.OrganizerName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(a => a.OrganizerType)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(a => a.ContactNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(a => a.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(a => a.Website)
                .HasMaxLength(500);

            entity.Property(a => a.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(a => a.SubmittedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(a => a.ReviewComment)
                .HasMaxLength(1000);

            entity.HasOne(a => a.User)
                .WithOne(u => u.OrganizerApplication)
                .HasForeignKey<OrganizerApplication>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Exactly one application per user is enforced at DB level
            entity.HasIndex(a => a.UserId)
                .IsUnique();
        });
    }
}
