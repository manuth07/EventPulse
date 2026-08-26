using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using EventPulse.IdentityService.Data;
using EventPulse.IdentityService.Models;

namespace EventPulse.IdentityService.Services;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailVerificationService> _logger;

    private const int CodeExpiryMinutes = 10;
    private const int MaxFailedAttempts = 5;
    private const int ResendCooldownSeconds = 60;

    public EmailVerificationService(
        ApplicationDbContext dbContext,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<EmailVerificationService> logger)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendVerificationCodeAsync(Guid userId, string email)
    {
        var now = DateTime.UtcNow;

        // Check cooldown
        var lastCode = await _dbContext.EmailVerificationCodes
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastCode != null && (now - lastCode.CreatedAt).TotalSeconds < ResendCooldownSeconds)
        {
            _logger.LogWarning("Resend cooldown active for user {UserId}", userId);
            return false;
        }

        // Invalidate old active codes (by expiring them immediately)
        var activeCodes = await _dbContext.EmailVerificationCodes
            .Where(x => x.UserId == userId && x.UsedAt == null && x.ExpiresAt > now)
            .ToListAsync();
            
        foreach (var code in activeCodes)
        {
            code.ExpiresAt = now;
        }

        // Generate cryptographically secure 6-digit code
        var rawCode = GenerateSecure6DigitCode();
        var hashedCode = HashCode(rawCode);

        // Store hash
        var verificationCode = new EmailVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = hashedCode,
            ExpiresAt = now.AddMinutes(CodeExpiryMinutes),
            CreatedAt = now,
            AttemptCount = 0
        };

        _dbContext.EmailVerificationCodes.Add(verificationCode);
        await _dbContext.SaveChangesAsync();

        // Send email
        // If this throws, it bubbles up, so the API can return a clean error without pretending it succeeded.
        await _emailSender.SendVerificationEmailAsync(email, rawCode);
        
        return true;
    }

    public async Task<VerificationResult> VerifyCodeAsync(Guid userId, string rawCode)
    {
        var now = DateTime.UtcNow;

        var activeCode = await _dbContext.EmailVerificationCodes
            .Where(x => x.UserId == userId && x.UsedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (activeCode == null)
        {
            return VerificationResult.Failure("This verification code has expired or is invalid. Request a new code.");
        }

        if (activeCode.AttemptCount >= MaxFailedAttempts)
        {
            // Block further attempts on this code
            activeCode.ExpiresAt = now;
            await _dbContext.SaveChangesAsync();
            return VerificationResult.Failure("Too many failed attempts. Please request a new code.");
        }

        var hashedInput = HashCode(rawCode);

        // Constant time comparison is preferred for hashes, but since it's a fixed length hash and we aren't highly sensitive to timing attacks on 6 digit OTPs, String.Equals is generally acceptable, but let's use CryptographicOperations if we compare byte arrays. Here we just compare strings.
        if (hashedInput != activeCode.CodeHash)
        {
            activeCode.AttemptCount++;
            await _dbContext.SaveChangesAsync();
            return VerificationResult.Failure("The verification code is incorrect.");
        }

        // Success
        activeCode.UsedAt = now;
        await _dbContext.SaveChangesAsync();
        
        return VerificationResult.Success();
    }

    private static string GenerateSecure6DigitCode()
    {
        // 100000 to 999999
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    private string HashCode(string code)
    {
        var secret = _configuration["Email:OtpSecret"] ?? "DefaultFallbackSecretForDevelopmentOnly";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(hashBytes);
    }
}
