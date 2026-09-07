using EventPulse.IdentityService.DTOs;

namespace EventPulse.IdentityService.Services;

public interface ILoginService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
}

public class LoginResult
{
    public bool Succeeded { get; private set; }
    public LoginResponse? Response { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }

    public static LoginResult Success(LoginResponse response) => new()
    {
        Succeeded = true,
        Response = response,
        StatusCode = 200
    };

    public static LoginResult InvalidCredentials() => new()
    {
        Succeeded = false,
        Code = "INVALID_CREDENTIALS",
        Message = "Invalid email or password.",
        StatusCode = 401
    };

    public static LoginResult EmailNotVerified() => new()
    {
        Succeeded = false,
        Code = "EMAIL_NOT_VERIFIED",
        Message = "Please verify your email before signing in.",
        StatusCode = 403
    };

    public static LoginResult AccountDisabled() => new()
    {
        Succeeded = false,
        Code = "ACCOUNT_DISABLED",
        Message = "Account has been disabled.",
        StatusCode = 403
    };
}
