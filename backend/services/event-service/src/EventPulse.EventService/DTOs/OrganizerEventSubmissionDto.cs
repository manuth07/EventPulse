namespace EventPulse.EventService.DTOs;

/// <summary>
/// DTO representing an event submission belonging to the authenticated Organizer.
/// Exposes dashboard-relevant fields across all lifecycle statuses.
/// </summary>
public class OrganizerEventSubmissionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }
    public string? VenueType { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? CoverUrl { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
}
