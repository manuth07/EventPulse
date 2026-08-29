namespace EventPulse.IdentityService.DTOs;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public AuthenticatedUserResponse User { get; set; } = new();
}
