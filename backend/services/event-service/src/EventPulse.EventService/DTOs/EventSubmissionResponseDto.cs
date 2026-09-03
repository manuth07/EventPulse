namespace EventPulse.EventService.DTOs;

/// <summary>
/// Response DTO returned to an Organizer after a successful event submission.
/// Exposes Status so the caller can confirm the event is in Pending state.
/// Does NOT expose internal review/audit fields.
/// </summary>
public class EventSubmissionResponseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid OrganizerId { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Public URL to the event poster. Null if no poster was uploaded.</summary>
    public string? ImageUrl { get; set; }
}
