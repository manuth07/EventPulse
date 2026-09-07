namespace EventPulse.IdentityService.Models;

/// <summary>
/// Centralised role-name constants for EventPulse.
/// Prevents magic strings being scattered across the codebase.
/// </summary>
public static class AppRoles
{
    public const string Customer = "Customer";
    public const string Organizer = "Organizer";
    public const string Administrator = "Administrator";
}
