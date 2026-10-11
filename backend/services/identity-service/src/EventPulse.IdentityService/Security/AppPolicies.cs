namespace EventPulse.IdentityService.Security;

/// <summary>
/// Centralized authorization policy name constants for EventPulse Identity Service.
/// Prevents magic strings being scattered across controllers.
/// </summary>
public static class AppPolicies
{
    /// <summary>
    /// Restricts access to authenticated Administrators with live account verification.
    /// Evaluates live database state to prevent stale JWT role bypass or deactivated admin usage.
    /// </summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Backward-compatible alias for AdminOnly policy.
    /// </summary>
    public const string AdministratorOnly = "AdministratorOnly";

    /// <summary>
    /// Restricts access to authenticated Organizers.
    /// </summary>
    public const string OrganizerOnly = "OrganizerOnly";
}
