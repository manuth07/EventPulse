using System.ComponentModel.DataAnnotations;

namespace EventPulse.IdentityService.DTOs;

/// <summary>
/// Request payload to initiate a password reset.
/// </summary>
public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Standard public response for password reset requests.
/// Returns identical generic messaging regardless of whether the email exists.
/// </summary>
public class ForgotPasswordResponse
{
    public string Message { get; set; } = "If an account exists for this email, a password reset link has been sent.";
}
