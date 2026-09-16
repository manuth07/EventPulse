namespace EventPulse.EventService;

/// <summary>
/// Named authorization policy constants for the Event Service.
/// Prevents magic strings being scattered across controllers.
/// 
/// Policies are evaluated against the signed EventPulse JWT;
/// the Event Service never queries the Identity database.
/// </summary>
public static class AppPolicies
{
    /// <summary>
    /// Allows only users with the Organizer role.
    /// Used for create/edit/manage event endpoints.
    /// </summary>
    public const string OrganizerOnly = "OrganizerOnly";

    /// <summary>
    /// Allows only users with the Administrator role.
    /// Used for event approval, user management endpoints.
    /// </summary>
    public const string AdministratorOnly = "AdministratorOnly";
}
