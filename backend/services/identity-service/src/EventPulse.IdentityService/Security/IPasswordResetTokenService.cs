namespace EventPulse.IdentityService.Security;

/// <summary>
/// Provides cryptographically secure token generation, hashing, and verification
/// for password-reset tokens.
/// Raw tokens are never stored in persistence and never logged.
/// </summary>
public interface IPasswordResetTokenService
{
    /// <summary>
    /// Generates a cryptographically secure random raw token suitable for URL transmission.
    /// </summary>
    string GenerateRawToken();

    /// <summary>
    /// Computes the secure SHA-256 hash of a raw token for database storage or indexed lookup.
    /// </summary>
    string HashToken(string rawToken);

    /// <summary>
    /// Verifies that a raw token matches an expected token hash using constant-time comparison.
    /// </summary>
    bool ValidateTokenHash(string rawToken, string expectedHash);
}
