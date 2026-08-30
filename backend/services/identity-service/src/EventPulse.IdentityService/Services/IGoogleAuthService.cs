using EventPulse.IdentityService.DTOs;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Result type for Google authentication operations.
/// Carries the outcome code, HTTP status, optional response payload, and optional safe message.
/// </summary>
public class GoogleAuthResult
{
    public bool Succeeded { get; private set; }
    public LoginResponse? Response { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }

    /// <summary>Populated for ACCOUNT_LINK_REQUIRED: signals the frontend to show the link-account form.</summary>
    public bool RequiresAccountLink { get; private set; }

    public static GoogleAuthResult Success(LoginResponse response) => new()
    {
        Succeeded = true,
        Response = response,
        StatusCode = 200
    };

    public static GoogleAuthResult AuthFailed(string message = "Google authentication failed.") => new()
    {
        Succeeded = false,
        Code = "GOOGLE_AUTH_FAILED",
        Message = message,
        StatusCode = 401
    };

    public static GoogleAuthResult EmailNotVerified() => new()
    {
        Succeeded = false,
        Code = "GOOGLE_EMAIL_NOT_VERIFIED",
        Message = "Google account email must be verified before signing in.",
        StatusCode = 403
    };

    public static GoogleAuthResult AccountDisabled() => new()
    {
        Succeeded = false,
        Code = "ACCOUNT_DISABLED",
        Message = "Account has been disabled.",
        StatusCode = 403
    };

    public static GoogleAuthResult AccountLinkRequired() => new()
    {
        Succeeded = false,
        Code = "ACCOUNT_LINK_REQUIRED",
        Message = "An EventPulse account already exists with this email.",
        StatusCode = 409,
        RequiresAccountLink = true
    };

    public static GoogleAuthResult ExternalLoginConflict() => new()
    {
        Succeeded = false,
        Code = "EXTERNAL_LOGIN_CONFLICT",
        Message = "A different Google account is already linked to this email.",
        StatusCode = 409
    };

    public static GoogleAuthResult InvalidCredentials() => new()
    {
        Succeeded = false,
        Code = "INVALID_CREDENTIALS",
        Message = "Invalid email or password.",
        StatusCode = 401
    };

    public static GoogleAuthResult ServerError(string message = "An error occurred. Please try again.") => new()
    {
        Succeeded = false,
        Code = "SERVER_ERROR",
        Message = message,
        StatusCode = 500
    };
}

public interface IGoogleAuthService
{
    Task<GoogleAuthResult> GoogleLoginAsync(string googleCredential);
    Task<GoogleAuthResult> LinkExistingAccountAsync(string googleCredential, string eventPulsePassword);
}
