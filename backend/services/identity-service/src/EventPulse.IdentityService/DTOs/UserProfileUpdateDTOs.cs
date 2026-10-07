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
