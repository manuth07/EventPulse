using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using EventPulse.IdentityService.Models;

namespace EventPulse.IdentityService.Security;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IOptions<JwtSettings> settingsOptions, ILogger<JwtTokenService> logger)
    {
        _settings = settingsOptions.Value;
        _logger = logger;
    }

    public (string Token, int ExpiresInSeconds) GenerateToken(ApplicationUser user, IList<string> roles)
    {
        ValidateJwtSettings(_settings);

        var keyBytes = Encoding.UTF8.GetBytes(_settings.Key);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("firstName", user.FirstName),
            new("lastName", user.LastName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwtString = tokenHandler.WriteToken(token);

        var expiresInSeconds = _settings.AccessTokenMinutes * 60;

        _logger.LogInformation("JWT successfully generated for user {UserId}", user.Id);

        return (jwtString, expiresInSeconds);
    }

    private static void ValidateJwtSettings(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Key))
        {
            throw new InvalidOperationException("JWT signing key is missing. Configure 'Jwt:Key' via .NET User Secrets or Environment Variables.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(settings.Key);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key is insecure. Minimum length required is 256 bits (32 characters).");
        }

        var lowerKey = settings.Key.ToLowerInvariant().Trim();
        if (lowerKey == "secret" || lowerKey == "123456" || lowerKey == "change-me" || lowerKey == "change_me" || lowerKey == "your-256-bit-secret")
        {
            throw new InvalidOperationException("JWT signing key is obviously insecure fallback text. Please set a secure random key.");
        }
    }
}
