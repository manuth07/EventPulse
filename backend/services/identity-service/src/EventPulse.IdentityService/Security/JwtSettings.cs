namespace EventPulse.IdentityService.Security;

public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "EventPulse.IdentityService";
    public string Audience { get; set; } = "EventPulse.Clients";
    public int AccessTokenMinutes { get; set; } = 60;
}
