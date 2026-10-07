namespace EventPulse.IdentityService.Models;

/// <summary>
/// Persists a hashed password-reset token for a user.
/// Raw tokens are never stored — only their cryptographic hash.
/// </summary>
public class PasswordResetToken
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The user this reset token was issued to.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Cryptographic hash (SHA-256) of the plain-text reset token.
    /// Never store the raw token.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this token expires.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC timestamp when this token was consumed/used. Null if not yet used.</summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>UTC timestamp when this token record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    public ApplicationUser User { get; set; } = null!;

    // -----------------------------------------------------------------------
    // Domain helpers
    // -----------------------------------------------------------------------

    /// <summary>Whether the token has already been consumed/used.</summary>
    public bool IsUsed => UsedAt.HasValue;

    /// <summary>Whether the token has passed its expiration time.</summary>
    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    /// <summary>Whether the token is currently active and can be used for password reset.</summary>
    public bool IsActive(DateTime utcNow) => !IsUsed && !IsExpired(utcNow);

    /// <summary>Consumes the token, marking it as used at the specified UTC timestamp.</summary>
    public void MarkAsUsed(DateTime? consumedAtUtc = null)
    {
        if (IsUsed)
        {
            throw new InvalidOperationException("Password reset token has already been used.");
        }

        UsedAt = consumedAtUtc ?? DateTime.UtcNow;
    }
}
