using Microsoft.AspNetCore.Identity;
using EventPulse.IdentityService.Models;

namespace EventPulse.IdentityService.Services;

public class SetPasswordResult
{
    public bool Succeeded { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }

    public static SetPasswordResult Success() => new() { Succeeded = true, StatusCode = 200 };

    public static SetPasswordResult AlreadySet() => new()
    {
        Succeeded = false,
        Code = "PASSWORD_ALREADY_SET",
        Message = "A password already exists. Use change-password to update it.",
        StatusCode = 409
    };

    public static SetPasswordResult ValidationFailed(IEnumerable<string> errors) => new()
    {
        Succeeded = false,
        Code = "VALIDATION_FAILED",
        Message = string.Join(" ", errors),
        StatusCode = 400
    };

    public static SetPasswordResult ServerError() => new()
    {
        Succeeded = false,
        Code = "SERVER_ERROR",
        Message = "An error occurred. Please try again.",
        StatusCode = 500
    };
}

public interface ISetPasswordService
{
    Task<SetPasswordResult> SetPasswordAsync(Guid userId, string password, string confirmPassword);
}

/// <summary>
/// Allows a Google-only account to optionally add an EventPulse password.
/// User identity is always derived from the authenticated JWT — never from a request body.
/// Password policy is enforced by ASP.NET Core Identity (not manually).
/// </summary>
public class SetPasswordService : ISetPasswordService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SetPasswordService> _logger;

    public SetPasswordService(UserManager<ApplicationUser> userManager, ILogger<SetPasswordService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<SetPasswordResult> SetPasswordAsync(Guid userId, string password, string confirmPassword)
    {
        if (password != confirmPassword)
        {
            return SetPasswordResult.ValidationFailed(["Passwords do not match."]);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            // Should not happen since user is derived from a valid JWT
            _logger.LogError("SetPassword: user {UserId} not found.", userId);
            return SetPasswordResult.ServerError();
        }

        // Do not overwrite an existing password via this endpoint
        if (await _userManager.HasPasswordAsync(user))
        {
            _logger.LogWarning("SetPassword rejected: user {UserId} already has a password.", userId);
            return SetPasswordResult.AlreadySet();
        }

        // Identity enforces the configured password policy (length, upper, lower, digit)
        var result = await _userManager.AddPasswordAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            _logger.LogWarning("SetPassword failed for user {UserId}: {Errors}", userId, string.Join(", ", errors));
            return SetPasswordResult.ValidationFailed(errors);
        }

        _logger.LogInformation("Password set successfully for user {UserId}.", userId);
        return SetPasswordResult.Success();
    }
}
