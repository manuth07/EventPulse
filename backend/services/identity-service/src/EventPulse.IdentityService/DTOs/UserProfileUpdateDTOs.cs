using System.ComponentModel.DataAnnotations;

namespace EventPulse.IdentityService.DTOs;

/// <summary>Request body for PUT /api/users/me/email</summary>
public class UpdateEmailRequest
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [MaxLength(256, ErrorMessage = "Email address cannot exceed 256 characters.")]
    public string NewEmail { get; set; } = string.Empty;
}

/// <summary>Request body for PUT /api/users/me/phone</summary>
public class UpdatePhoneRequest
{
    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^\+?[0-9\s\-()]{7,20}$", ErrorMessage = "Please enter a valid phone number (7-20 digits).")]
    public string NewPhoneNumber { get; set; } = string.Empty;
}

/// <summary>Request body for POST /api/users/me/change-password (EP-26 Phase 4)</summary>
public class ChangePasswordRequest
{
    /// <summary>Current password for verification (optional for Google-only users without password).</summary>
    public string? CurrentPassword { get; set; }

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password confirmation is required.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
