namespace EventPulse.IdentityService.DTOs;

public class AuthenticatedUserResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    /// <summary>True when the user has completed their EventPulse profile (phone + country).</summary>
    public bool ProfileCompleted { get; set; }
    /// <summary>True when the account has a password hash (useful for account-security UI).</summary>
    public bool HasPassword { get; set; }
}
