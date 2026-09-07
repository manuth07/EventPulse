namespace EventPulse.EventService.Models;

public class Event
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public decimal Price { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Pending;
    public Guid OrganizerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    /// <summary>
    /// Administrator feedback or rejection reason.
    /// Preserved across resubmissions so the Organizer can view previous feedback.
    /// </summary>
    public string? ReviewComment { get; set; }
    /// <summary>
    /// Blob Storage reference key for the portrait event poster (e.g. "event-posters/abc123.webp").
    /// Null for seeded/legacy events without a poster.
    /// The full URL is resolved by IEventImageStorage at query time.
    /// </summary>
    public string? ImageBlobName { get; set; }

    /// <summary>
    /// Event category (e.g. "Musical Concert", "Conference", "Workshop", etc.).
    /// Nullable for legacy events created prior to this field.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Event venue environment type ("Indoor" or "Outdoor").
    /// Nullable for legacy events created prior to this field.
    /// </summary>
    public string? VenueType { get; set; }

    /// <summary>
    /// Blob Storage reference key for the wide event cover/banner (e.g. "event-covers/xyz789.webp").
    /// Used as the hero header on the public Event Details page.
    /// Null for seeded/legacy events without a dedicated cover banner.
    /// The full URL is resolved by IEventImageStorage at query time.
    /// </summary>
    public string? CoverBlobName { get; set; }
}
