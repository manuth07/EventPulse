namespace EventPulse.IdentityService.Models;

/// <summary>
/// Represents a Customer request to be granted the Organizer role.
/// Enforces one application lifecycle per user.
/// </summary>
public class OrganizerApplication
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string OrganizerName { get; set; } = string.Empty;

    public OrganizerType OrganizerType { get; set; }

    public string ContactNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Website { get; set; }

    public OrganizerApplicationStatus Status { get; set; } = OrganizerApplicationStatus.Pending;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public string? ReviewComment { get; set; }

    // Navigation
    public ApplicationUser? User { get; set; }
}
