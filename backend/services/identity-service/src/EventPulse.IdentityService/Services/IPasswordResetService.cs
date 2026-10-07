namespace EventPulse.IdentityService.Services;

/// <summary>
/// Service interface handling password reset request and token validation workflows.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Processes a password reset request. Generates and stores a hashed token if the user exists.
    /// Returns a generic message to prevent account/email enumeration.
    /// </summary>
    Task<RequestPasswordResetResult> RequestPasswordResetAsync(string email);

    /// <summary>
    /// Validates whether a raw password reset token is active, unexpired, and unconsumed.
    /// Does NOT consume the token.
    /// </summary>
    Task<ValidateResetTokenResult> ValidateResetTokenAsync(string token);

    /// <summary>
    /// Completes the password reset by validating the token, enforcing password policy,
    /// hashing and updating the user's password, and consuming the reset token.
    /// </summary>
    Task<ResetPasswordResult> ResetPasswordAsync(EventPulse.IdentityService.DTOs.ResetPasswordRequest request);
}

/// <summary>
/// Result for a password reset request operation.
/// </summary>
public class RequestPasswordResetResult
{
    public bool Succeeded { get; private set; }
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    /// Raw token prepared for internal forwarding to the email service in Part 3.
    /// Never sent across public API responses or logged.
    /// </summary>
    public string? RawToken { get; private set; }

    public static RequestPasswordResetResult GenericResponse(string? rawToken = null) => new()
    {
        Succeeded = true,
        Message = "If an account exists for this email, a password reset link has been sent.",
        RawToken = rawToken
    };
}

/// <summary>
/// Result for a reset token validation operation.
/// </summary>
public class ValidateResetTokenResult
{
    public bool IsValid { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }

    public static ValidateResetTokenResult Valid(string message = "The password reset token is valid.") => new()
    {
        IsValid = true,
        StatusCode = 200,
        Message = message
    };

    public static ValidateResetTokenResult Invalid(string code = "INVALID_TOKEN", string message = "The password reset link is invalid or has expired.") => new()
    {
        IsValid = false,
        StatusCode = 400,
        Code = code,
        Message = message
    };

    public static ValidateResetTokenResult Expired() => Invalid(
        code: "EXPIRED_TOKEN",
        message: "The password reset link has expired. Please request a new one."
    );

    public static ValidateResetTokenResult AlreadyUsed() => Invalid(
        code: "TOKEN_ALREADY_USED",
        message: "The password reset link has already been used. Please request a new one."
    );
}

/// <summary>
/// Result for a password reset completion operation.
/// </summary>
public class ResetPasswordResult
{
    public bool Succeeded { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public IEnumerable<string> Errors { get; private set; } = [];
    public int StatusCode { get; private set; }

    public static ResetPasswordResult Success() => new()
    {
        Succeeded = true,
        StatusCode = 200,
        Message = "Password has been reset successfully. You can now log in with your new password."
    };

    public static ResetPasswordResult InvalidToken(string message = "The password reset link is invalid or has expired.") => new()
    {
        Succeeded = false,
        Code = "INVALID_TOKEN",
        Message = message,
        StatusCode = 400
    };

    public static ResetPasswordResult Expired() => new()
    {
        Succeeded = false,
        Code = "EXPIRED_TOKEN",
        Message = "The password reset link has expired. Please request a new one.",
        StatusCode = 400
    };

    public static ResetPasswordResult AlreadyUsed() => new()
    {
        Succeeded = false,
        Code = "TOKEN_ALREADY_USED",
        Message = "The password reset link has already been used. Please request a new one.",
        StatusCode = 400
    };

    public static ResetPasswordResult PasswordsDoNotMatch() => new()
    {
        Succeeded = false,
        Code = "PASSWORDS_DO_NOT_MATCH",
        Message = "Passwords do not match.",
        StatusCode = 400
    };

    public static ResetPasswordResult InvalidPassword(IEnumerable<string> errors) => new()
    {
        Succeeded = false,
        Code = "INVALID_PASSWORD",
        Message = "Password does not meet the security requirements.",
        Errors = errors,
        StatusCode = 400
    };

    public static ResetPasswordResult ServerError(string message = "An error occurred while resetting the password. Please try again.") => new()
    {
        Succeeded = false,
        Code = "SERVER_ERROR",
        Message = message,
        StatusCode = 500
    };
}
