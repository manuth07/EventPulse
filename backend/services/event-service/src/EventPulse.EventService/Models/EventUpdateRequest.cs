namespace EventPulse.EventService.Models;

/// <summary>
/// Represents an Organizer's proposed modifications to an existing approved/published Event.
/// The approved Event remains unchanged while this request is in Pending status.
/// Upon Administrator approval in Phase 2, proposed changes are applied to the live Event.
/// </summary>
public class EventUpdateRequest
{
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key referencing the live approved Event being modified.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Navigation property to the target Event.
    /// </summary>
    public Event Event { get; set; } = null!;

    /// <summary>
    /// Identifier of the Organizer who submitted the update request.
    /// Derived from authenticated JWT claims.
    /// </summary>
    public Guid OrganizerId { get; set; }

    /// <summary>
    /// Lifecycle status of this update proposal (Pending, Approved, Rejected).
    /// </summary>
    public EventUpdateRequestStatus Status { get; set; } = EventUpdateRequestStatus.Pending;

    /// <summary>
    /// Timestamp when this update request was submitted.
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this request was reviewed by an Administrator (null while Pending).
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Identifier of the Administrator who reviewed this request (null while Pending).
    /// </summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>
    /// Administrator feedback or review notes.
    /// </summary>
    public string? ReviewComment { get; set; }

    // =========================================================================
    // PROPOSED EVENT ATTRIBUTES
    // Captured independently from the live Event record to preserve live state.
    // =========================================================================

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Venue { get; set; } = string.Empty;

    public DateTime EventDate { get; set; }

    public string? Category { get; set; }

    public string? VenueType { get; set; }

    /// <summary>
    /// Blob reference key for proposed replacement portrait poster.
    /// Retains existing Event.ImageBlobName if poster was not replaced.
    /// </summary>
    public string? ImageBlobName { get; set; }

    /// <summary>
    /// Blob reference key for proposed replacement wide cover banner.
    /// Retains existing Event.CoverBlobName if cover was not replaced.
    /// </summary>
    public string? CoverBlobName { get; set; }
}
