using System.ComponentModel.DataAnnotations;

namespace EventPulse.IdentityService.DTOs;

public class ResendVerificationRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;
}
