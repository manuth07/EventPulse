namespace EventPulse.IdentityService.Models;

/// <summary>
/// Historical record of administrative user state changes (EP-57 / EP-371).
/// Persisted atomically with state changes to guarantee audit consistency.
/// </summary>
public class AdminAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>GUID of the administrator who executed the action.</summary>
    public Guid ActorAdminId { get; set; }

    /// <summary>Email of the administrator who executed the action.</summary>
    public string ActorEmail { get; set; } = string.Empty;

    /// <summary>GUID of the target user whose state was modified.</summary>
    public Guid TargetUserId { get; set; }

    /// <summary>Email of the target user.</summary>
    public string TargetEmail { get; set; } = string.Empty;

    /// <summary>Action identifier: ADMIN_USER_ACTIVATED, ADMIN_USER_DEACTIVATED, ADMIN_USER_ROLE_CHANGED.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Serialized JSON snapshot of relevant fields before the change.</summary>
    public string? PreviousState { get; set; }

    /// <summary>Serialized JSON snapshot of relevant fields after the change.</summary>
    public string? NewState { get; set; }

    /// <summary>Optional administrator justification or reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Execution outcome (SUCCESS or FAILURE).</summary>
    public string Outcome { get; set; } = "SUCCESS";

    /// <summary>Client IP address captured from HTTP context.</summary>
    public string? IpAddress { get; set; }

    /// <summary>UTC timestamp when the administrative action occurred.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
