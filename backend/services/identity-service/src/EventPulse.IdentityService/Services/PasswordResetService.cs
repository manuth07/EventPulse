using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Security;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Implements password reset initiation and token validation workflows.
/// Never logs or exposes plain-text reset tokens.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    private const int DefaultTokenExpiryMinutes = 30;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordResetTokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IPasswordResetTokenService tokenService,
        IConfiguration configuration,
        ILogger<PasswordResetService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RequestPasswordResetResult> RequestPasswordResetAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RequestPasswordResetResult.GenericResponse();
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        // Security requirement: anti-enumeration.
        // Return identical response when user does not exist or account is inactive.
        if (user == null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for unrecognized or inactive email.");
            return RequestPasswordResetResult.GenericResponse();
        }

        var now = DateTime.UtcNow;

        // Invalidate any existing active tokens for this user
        var activeTokens = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync();

        foreach (var existingToken in activeTokens)
        {
            existingToken.ExpiresAt = now;
        }

        // Generate cryptographically secure raw token and compute SHA-256 hash
        var rawToken = _tokenService.GenerateRawToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        var expiryMinutes = _configuration.GetValue<int?>("PasswordReset:TokenExpiryMinutes") ?? DefaultTokenExpiryMinutes;
        if (expiryMinutes <= 0)
        {
            expiryMinutes = DefaultTokenExpiryMinutes;
        }

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = now.AddMinutes(expiryMinutes),
            CreatedAt = now
        };

        _dbContext.PasswordResetTokens.Add(resetToken);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Password reset token generated and persisted for user {UserId}.", user.Id);

        // Hook for Part 3: rawToken is held in internal service result to pass to email dispatcher
        return RequestPasswordResetResult.GenericResponse(rawToken);
    }

    public async Task<ValidateResetTokenResult> ValidateResetTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ValidateResetTokenResult.Invalid(
                code: "INVALID_TOKEN",
                message: "The password reset token is required."
            );
        }

        var tokenHash = _tokenService.HashToken(token.Trim());

        var tokenRecord = await _dbContext.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (tokenRecord == null)
        {
            _logger.LogWarning("Password reset token validation failed: token record not found.");
            return ValidateResetTokenResult.Invalid(
                code: "INVALID_TOKEN",
                message: "The password reset link is invalid or has expired."
            );
        }

        if (tokenRecord.IsUsed)
        {
            _logger.LogWarning("Password reset token validation failed: token already consumed.");
            return ValidateResetTokenResult.AlreadyUsed();
        }

        var now = DateTime.UtcNow;
        if (tokenRecord.IsExpired(now))
        {
            _logger.LogWarning("Password reset token validation failed: token expired.");
            return ValidateResetTokenResult.Expired();
        }

        if (!_tokenService.ValidateTokenHash(token.Trim(), tokenRecord.TokenHash))
        {
            _logger.LogWarning("Password reset token validation failed: cryptographic hash mismatch.");
            return ValidateResetTokenResult.Invalid(
                code: "INVALID_TOKEN",
                message: "The password reset link is invalid or has expired."
            );
        }

        if (tokenRecord.User != null && !tokenRecord.User.IsActive)
        {
            _logger.LogWarning("Password reset token validation failed: associated account is inactive.");
            return ValidateResetTokenResult.Invalid(
                code: "INVALID_TOKEN",
                message: "The password reset link is invalid or has expired."
            );
        }

        // Token is valid and NOT consumed. UsedAt remains null.
        return ValidateResetTokenResult.Valid();
    }
}
