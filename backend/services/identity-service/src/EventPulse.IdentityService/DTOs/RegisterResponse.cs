namespace EventPulse.IdentityService.DTOs;

/// <summary>
/// Outbound response returned to the client on successful registration.
/// Never includes PasswordHash, SecurityStamp, or other identity internals.
/// </summary>
public class RegisterResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public bool VerificationRequired { get; set; }
    public bool VerificationEmailSent { get; set; }
}
