using Microsoft.AspNetCore.Identity;

namespace EventPulse.IdentityService.Models;

/// <summary>
/// EventPulse account entity extending ASP.NET Core Identity.
/// Supports both email/password and Google authentication.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    // -----------------------------------------------------------------------
    // Identity-provided fields (do NOT redeclare):
    // Id, UserName, NormalizedUserName, Email, NormalizedEmail,
    // EmailConfirmed, PasswordHash (nullable → supports Google-only accounts),
    // SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed,
    // TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // EventPulse-specific profile fields
    // -----------------------------------------------------------------------

    /// <summary>First name of the account holder.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Last name of the account holder.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. LK, SG, GB).</summary>
    public string? CountryCode { get; set; }

    /// <summary>Whether the user has completed the onboarding profile step.</summary>
    public bool ProfileCompleted { get; set; } = false;

    /// <summary>Whether the account is active. Inactive accounts cannot authenticate.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    public ICollection<EmailVerificationCode> EmailVerificationCodes { get; set; } = [];
}
