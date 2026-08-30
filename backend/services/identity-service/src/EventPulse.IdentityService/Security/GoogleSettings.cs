namespace EventPulse.IdentityService.Security;

/// <summary>
/// Configuration for Google Identity Services token validation.
/// Populated from configuration section "Google".
/// Values are set via .NET User Secrets (development) or Azure App Service env vars (production).
/// NEVER hard-code real client IDs here.
/// </summary>
public class GoogleSettings
{
    /// <summary>
    /// Google OAuth 2.0 Client ID for this environment.
    /// Must match the VITE_GOOGLE_CLIENT_ID used in the frontend build.
    /// Development: set via User Secrets "Google:ClientId"
    /// Production: set via Azure App Service env var "Google__ClientId"
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
}
