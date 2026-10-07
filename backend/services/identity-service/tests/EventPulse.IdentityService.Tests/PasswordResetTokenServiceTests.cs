using System.Security.Cryptography;
using System.Text;
using EventPulse.IdentityService.Security;
using Xunit;

namespace EventPulse.IdentityService.Tests;

public class PasswordResetTokenServiceTests
{
    private readonly PasswordResetTokenService _sut = new();

    [Fact]
    public void GenerateRawToken_Returns64HexCharacters()
    {
        var token = _sut.GenerateRawToken();

        Assert.NotNull(token);
        Assert.Equal(64, token.Length); // 32 bytes * 2 hex chars per byte
        Assert.Matches("^[0-9a-f]{64}$", token);
    }

    [Fact]
    public void GenerateRawToken_ProducesUniqueTokens()
    {
        var tokens = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var token = _sut.GenerateRawToken();
            Assert.True(tokens.Add(token), "Generated token was not unique.");
        }
    }

    [Fact]
    public void HashToken_ComputesDeterministicSha256HexHash()
    {
        const string rawToken = "test_secure_raw_token_value_123456789";

        var hash1 = _sut.HashToken(rawToken);
        var hash2 = _sut.HashToken(rawToken);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash1);

        // Verify matches SHA-256 computation
        var expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        var expectedHex = Convert.ToHexString(expectedBytes).ToLowerInvariant();
        Assert.Equal(expectedHex, hash1);
    }

    [Fact]
    public void HashToken_DifferentTokensProduceDifferentHashes()
    {
        var tokenA = _sut.GenerateRawToken();
        var tokenB = _sut.GenerateRawToken();

        var hashA = _sut.HashToken(tokenA);
        var hashB = _sut.HashToken(tokenB);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void HashToken_TrimsLeadingAndTrailingWhitespace()
    {
        var token = _sut.GenerateRawToken();
        var paddedToken = $"  {token}\t\n";

        var hashDirect = _sut.HashToken(token);
        var hashPadded = _sut.HashToken(paddedToken);

        Assert.Equal(hashDirect, hashPadded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HashToken_NullOrWhitespace_ThrowsArgumentException(string? invalidToken)
    {
        Assert.Throws<ArgumentException>(() => _sut.HashToken(invalidToken!));
    }

    [Fact]
    public void ValidateTokenHash_WithMatchingTokenAndHash_ReturnsTrue()
    {
        var rawToken = _sut.GenerateRawToken();
        var hash = _sut.HashToken(rawToken);

        var isValid = _sut.ValidateTokenHash(rawToken, hash);

        Assert.True(isValid);
    }

    [Fact]
    public void ValidateTokenHash_WithMismatchedToken_ReturnsFalse()
    {
        var rawToken = _sut.GenerateRawToken();
        var differentToken = _sut.GenerateRawToken();
        var hash = _sut.HashToken(rawToken);

        var isValid = _sut.ValidateTokenHash(differentToken, hash);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData(null, "somehash")]
    [InlineData("", "somehash")]
    [InlineData("sometoken", null)]
    [InlineData("sometoken", "")]
    [InlineData("   ", "   ")]
    public void ValidateTokenHash_WithInvalidInputs_ReturnsFalse(string? rawToken, string? hash)
    {
        var isValid = _sut.ValidateTokenHash(rawToken!, hash!);

        Assert.False(isValid);
    }

    [Fact]
    public void RawToken_IsNeverIdenticalToHash()
    {
        var rawToken = _sut.GenerateRawToken();
        var hash = _sut.HashToken(rawToken);

        Assert.NotEqual(rawToken, hash);
    }
}
