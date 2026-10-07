using System.Security.Cryptography;
using System.Text;

namespace EventPulse.IdentityService.Security;

/// <summary>
/// Implementation of IPasswordResetTokenService.
/// Generates 256-bit cryptographically secure random tokens and SHA-256 hashes.
/// Strictly enforces that raw tokens are never logged or stored.
/// </summary>
public class PasswordResetTokenService : IPasswordResetTokenService
{
    private const int TokenEntropyBytes = 32; // 256 bits of entropy

    public string GenerateRawToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(TokenEntropyBytes);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }

    public string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentException("Reset token cannot be null or whitespace.", nameof(rawToken));
        }

        var tokenBytes = Encoding.UTF8.GetBytes(rawToken.Trim());
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public bool ValidateTokenHash(string rawToken, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var computedHash = HashToken(rawToken);
        var computedBytes = Encoding.UTF8.GetBytes(computedHash);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash.Trim().ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(computedBytes, expectedBytes);
    }
}
