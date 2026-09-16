namespace EventPulse.EventService.DTOs;

/// <summary>
/// Focused DTO for Administrator review of an Event Cancellation Request (EP-35 / US-15).
/// Exposes event operational details, organizer identity, cancellation reason, submission timestamp,
/// and ticket sales summary.
/// </summary>
public class AdminEventCancellationReviewDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid OrganizerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }

    // Live Event Details
    public string EventTitle { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string EventVenue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public decimal EventPrice { get; set; }
    public string EventStatus { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? VenueType { get; set; }
    public string? ImageUrl { get; set; }
    public string? CoverUrl { get; set; }

    // Ticket Sales Summary
    public int TotalTicketsSold { get; set; }
    public int TotalCapacity { get; set; }
}
