namespace EventPulse.IdentityService.Security;

/// <summary>
/// Configuration for the one-time Administrator bootstrap mechanism.
/// 
/// Values MUST be supplied via .NET User Secrets (local) or
/// Azure App Service application settings (production).
/// 
/// Never commit real credentials to appsettings.json or any source file.
/// 
/// Production lifecycle:
///   1. Set Enabled = true before first deployment.
///   2. After the admin account is confirmed in the database, set Enabled = false
///      and remove the Password value from Azure configuration.
///   3. The bootstrap is idempotent; it will NOT overwrite an existing admin.
/// </summary>
public class AdminBootstrapSettings
{
    /// <summary>
    /// Set to true to enable administrator provisioning on startup.
    /// Set to false (or omit) in production after the admin account exists.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Email address for the bootstrap Administrator account.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Initial password for the bootstrap Administrator account.
    /// Must satisfy Identity password policy (≥8 chars, upper, lower, digit).
    /// Remove from Azure configuration once the admin account is confirmed.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Administrator's first name.</summary>
    public string FirstName { get; set; } = "Admin";

    /// <summary>Administrator's last name.</summary>
    public string LastName { get; set; } = "EventPulse";
}
