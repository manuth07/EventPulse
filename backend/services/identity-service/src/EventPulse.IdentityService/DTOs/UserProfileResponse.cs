namespace EventPulse.IdentityService.DTOs;

/// <summary>
/// Authoritative safe user profile response (EP-26 / US-06).
/// Contains only safe customer account information.
/// </summary>
public class UserProfileResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CountryCode { get; set; }
    public bool ProfileCompleted { get; set; }
    public bool HasPassword { get; set; }
    public string Role { get; set; } = "Customer";
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
