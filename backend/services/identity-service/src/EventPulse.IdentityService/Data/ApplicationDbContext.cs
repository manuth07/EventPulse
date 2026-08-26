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
    }
}
