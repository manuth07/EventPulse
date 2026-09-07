namespace EventPulse.IdentityService.Services;

public interface IEmailVerificationService
{
    /// <summary>
    /// Generates a new 6-digit OTP, stores its hash, and sends it via email.
    /// Invalidate any existing active codes for the user.
    /// </summary>
    Task<bool> SendVerificationCodeAsync(Guid userId, string email);

    /// <summary>
    /// Validates the provided OTP.
    /// </summary>
    Task<VerificationResult> VerifyCodeAsync(Guid userId, string code);
}

public class VerificationResult
{
    public bool Succeeded { get; init; }
    public bool IsRateLimited { get; init; }
    public string? ErrorMessage { get; init; }

    public static VerificationResult Success() => new() { Succeeded = true };
    public static VerificationResult Failure(string message) => new() { Succeeded = false, ErrorMessage = message };
    public static VerificationResult RateLimited(string message) => new() { Succeeded = false, IsRateLimited = true, ErrorMessage = message };
}
