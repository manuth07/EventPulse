using System.ComponentModel.DataAnnotations;

namespace EventPulse.IdentityService.DTOs;

/// <summary>Request body for POST /api/auth/google</summary>
public class GoogleLoginRequest
{
    /// <summary>Google Identity Services ID token (credential) received from the frontend.</summary>
    [Required(ErrorMessage = "Google credential is required.")]
    public string Credential { get; set; } = string.Empty;
}

/// <summary>Request body for POST /api/auth/google/link-existing</summary>
public class GoogleLinkRequest
{
    /// <summary>Google Identity Services ID token to re-validate on the link request.</summary>
    [Required(ErrorMessage = "Google credential is required.")]
    public string Credential { get; set; } = string.Empty;

    /// <summary>Existing EventPulse account password for ownership verification.</summary>
    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Request body for POST /api/auth/set-password</summary>
public class SetPasswordRequest
{
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Request body for PUT /api/users/me/profile</summary>
public class CompleteProfileRequest
{
    [Required(ErrorMessage = "Phone number is required.")]
    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Country code is required.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Country code must be exactly 2 characters.")]
    public string CountryCode { get; set; } = string.Empty;
}
