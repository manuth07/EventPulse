using System.ComponentModel.DataAnnotations;

namespace EventPulse.IdentityService.DTOs;

/// <summary>
/// Request payload to validate whether a password-reset token is valid and unconsumed.
/// </summary>
public class ValidateResetTokenRequest
{
    [Required(ErrorMessage = "Reset token is required.")]
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Response payload indicating whether a reset token is usable.
/// </summary>
public class ValidateResetTokenResponse
{
    public bool IsValid { get; set; }
    public string? Code { get; set; }
    public string Message { get; set; } = string.Empty;
}
