namespace EventPulse.EventService.DTOs;

/// <summary>
/// DTO representing an Event Update Request.
/// Exposes the proposed editable fields, lifecycle review status, public image URLs,
/// and change detection flags comparing proposed values to the approved event.
/// </summary>
public class EventUpdateRequestDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid OrganizerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }

    // =========================================================================
    // PROPOSED EVENT ATTRIBUTES
    // =========================================================================

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string? Category { get; set; }
    public string? VenueType { get; set; }

    public string? ImageBlobName { get; set; }
    public string? ImageUrl { get; set; }

    public string? CoverBlobName { get; set; }
    public string? CoverUrl { get; set; }

    // =========================================================================
    // CHANGE DETECTION FLAGS (Step 6)
    // Structured for future customer/ticket-holder notifications on major changes.
    // =========================================================================

    public bool HasTitleChanged { get; set; }
    public bool HasDescriptionChanged { get; set; }
    public bool HasVenueChanged { get; set; }
    public bool HasDateChanged { get; set; }
    public bool HasCategoryChanged { get; set; }
    public bool HasVenueTypeChanged { get; set; }
    public bool HasImageChanged { get; set; }
    public bool HasCoverChanged { get; set; }

    /// <summary>
    /// Indicates whether a major change occurred (Venue or Date changed),
    /// which will trigger customer notifications in later phases.
    /// </summary>
    public bool IsMajorChange { get; set; }
}
