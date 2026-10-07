using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public async Task<ResetPasswordResult> ResetPasswordAsync(DTOs.ResetPasswordRequest request)
    {
        if (request == null)
        {
            return ResetPasswordResult.InvalidToken();
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return ResetPasswordResult.InvalidToken("Reset token is required.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return ResetPasswordResult.PasswordsDoNotMatch();
        }

        var tokenHash = _tokenService.HashToken(request.Token.Trim());

        var tokenRecord = await _dbContext.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (tokenRecord == null)
        {
            _logger.LogWarning("Password reset completion rejected: token record not found.");
            return ResetPasswordResult.InvalidToken();
        }

        if (tokenRecord.IsUsed)
        {
            _logger.LogWarning("Password reset completion rejected: token already consumed.");
            return ResetPasswordResult.AlreadyUsed();
        }

        var now = DateTime.UtcNow;
        if (tokenRecord.IsExpired(now))
        {
            _logger.LogWarning("Password reset completion rejected: token expired.");
            return ResetPasswordResult.Expired();
        }

        if (!_tokenService.ValidateTokenHash(request.Token.Trim(), tokenRecord.TokenHash))
        {
            _logger.LogWarning("Password reset completion rejected: cryptographic hash mismatch.");
            return ResetPasswordResult.InvalidToken();
        }

        var user = tokenRecord.User;
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Password reset completion rejected: user inactive or missing.");
            return ResetPasswordResult.InvalidToken();
        }

        // Validate password against configured Identity password policy rules
        var policyErrors = new List<string>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var valResult = await validator.ValidateAsync(_userManager, user, request.NewPassword);
            if (!valResult.Succeeded)
            {
                policyErrors.AddRange(valResult.Errors.Select(e => e.Description));
            }
        }

        if (policyErrors.Count > 0)
        {
            _logger.LogWarning("Password reset rejected for user {UserId}: password policy check failed.", user.Id);
            return ResetPasswordResult.InvalidPassword(policyErrors);
        }

        IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsRelational())
        {
            transaction = await _dbContext.Database.BeginTransactionAsync();
        }

        try
        {
            // Consume the single-use token
            tokenRecord.MarkAsUsed(now);

            // Invalidate any other active reset tokens for this user
            var otherActiveTokens = await _dbContext.PasswordResetTokens
                .Where(t => t.UserId == user.Id && t.Id != tokenRecord.Id && t.UsedAt == null && t.ExpiresAt > now)
                .ToListAsync();

            foreach (var other in otherActiveTokens)
            {
                other.ExpiresAt = now;
            }

            await _dbContext.SaveChangesAsync();

            // Update user password using ASP.NET Core Identity
            if (await _userManager.HasPasswordAsync(user))
            {
                var removeResult = await _userManager.RemovePasswordAsync(user);
                if (!removeResult.Succeeded)
                {
                    if (transaction != null) await transaction.RollbackAsync();
                    tokenRecord.UsedAt = null;
                    await _dbContext.SaveChangesAsync();

                    var errors = removeResult.Errors.Select(e => e.Description);
                    _logger.LogError("Failed to remove old password for user {UserId}.", user.Id);
                    return ResetPasswordResult.ServerError();
                }
            }

            var addResult = await _userManager.AddPasswordAsync(user, request.NewPassword);
            if (!addResult.Succeeded)
            {
                if (transaction != null) await transaction.RollbackAsync();
                tokenRecord.UsedAt = null;
                await _dbContext.SaveChangesAsync();

                var errors = addResult.Errors.Select(e => e.Description).ToList();
                _logger.LogError("Failed to add new password for user {UserId}.", user.Id);
                return ResetPasswordResult.InvalidPassword(errors);
            }

            await _userManager.UpdateSecurityStampAsync(user);

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            _logger.LogInformation("Password reset successfully completed for user {UserId}.", user.Id);
            return ResetPasswordResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null) await transaction.RollbackAsync();
            _logger.LogWarning("Concurrency conflict detected while consuming password reset token for user {UserId}.", user.Id);
            return ResetPasswordResult.AlreadyUsed();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already been used"))
        {
            if (transaction != null) await transaction.RollbackAsync();
            _logger.LogWarning("Token already consumed: {Message}", ex.Message);
            return ResetPasswordResult.AlreadyUsed();
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync();
            _logger.LogError(ex, "Unexpected error completing password reset for user {UserId}.", user.Id);
            return ResetPasswordResult.ServerError();
        }
        finally
        {
            transaction?.Dispose();
        }
    }
}
