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
    /// Blob Storage reference key (e.g. "events/abc123.jpg").
    /// Null for seeded/legacy events without a poster.
    /// The full URL is resolved by IEventImageStorage at query time.
    /// </summary>
    public string? ImageBlobName { get; set; }
}
