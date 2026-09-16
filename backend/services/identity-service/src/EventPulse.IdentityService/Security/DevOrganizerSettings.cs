namespace EventPulse.IdentityService.Security;

/// <summary>
/// Configuration for seeding a controlled development/demo Organizer account.
///
/// DEVELOPMENT ONLY — never use in production.
///
/// Lifecycle:
///   1. Set Enabled = true and provide credentials via User Secrets.
///   2. The seeder creates the account with Organizer role on next startup (idempotent).
///   3. Remove or disable after Sprint 1 / before production deployment.
///
/// Azure production note:
///   This mechanism does NOT need to be enabled in production.
///   Production Organizer onboarding is a separate future story.
/// </summary>
public class DevOrganizerSettings
{
    /// <summary>Set true in local development only.</summary>
    public bool Enabled { get; set; } = false;

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = "Organizer";
    public string LastName { get; set; } = "Dev";
}
