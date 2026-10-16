using System.ComponentModel.DataAnnotations;

namespace EventPulse.IdentityService.DTOs;

/// <summary>
/// Request payload to set a new password using a validated reset token.
/// </summary>
public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Reset token is required.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// Forgiving alias for clients that send "Password" instead of "NewPassword".
    /// </summary>
    public string Password
    {
        get => NewPassword;
        set
        {
            if (string.IsNullOrEmpty(NewPassword))
            {
                NewPassword = value;
            }
        }
    }
}

/// <summary>
/// Response payload for successful password reset completion.
/// </summary>
public class ResetPasswordResponse
{
    public string Message { get; set; } = "Password has been reset successfully. You can now log in with your new password.";
}
