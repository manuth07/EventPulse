namespace EventPulse.EventService.DTOs;

/// <summary>
/// Snapshot of event values used in side-by-side Administrator review comparisons.
/// </summary>
public class EventValuesDto
{
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
}

/// <summary>
/// DTO representing an Event Update Request for Administrator review (EP-210 / US-14).
/// Provides current live event values alongside proposed values with field change detection
/// and attendance-impacting major change flags.
/// </summary>
public class AdminEventUpdateComparisonDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid OrganizerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }

    /// <summary>
    /// Current approved values currently displayed to the public.
    /// </summary>
    public EventValuesDto Current { get; set; } = new();

    /// <summary>
    /// Proposed values requested by the organizer awaiting admin review.
    /// </summary>
    public EventValuesDto Proposed { get; set; } = new();

    // =========================================================================
    // FIELD CHANGE DETECTION FLAGS
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
    /// Indicates whether any attendance-impacting detail (venue or date/time) changed.
    /// Used by administrators to evaluate attendee impact before approval.
    /// </summary>
    public bool IsMajorChange => HasVenueChanged || HasDateChanged;
}
