namespace EventPulse.IdentityService.Models;

/// <summary>
/// Persists a hashed email verification code (OTP) for a user.
/// Supports the future email-confirmation flow.
/// Raw codes are never stored — only their hash.
/// </summary>
public class EmailVerificationCode
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The user this code was issued to.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// SHA-256 (or equivalent) hash of the plain-text verification code.
    /// Never store the raw code.
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this code expires.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Number of failed verification attempts against this code.</summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>UTC timestamp when this code was successfully used. Null if not yet used.</summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>UTC timestamp when this code record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    public ApplicationUser User { get; set; } = null!;
}
