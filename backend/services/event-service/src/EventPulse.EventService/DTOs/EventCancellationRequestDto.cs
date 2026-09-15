namespace EventPulse.EventService.DTOs;

/// <summary>
/// Representation of an Event Cancellation Request returned to Organizers and Administrators (EP-35 US-15).
/// </summary>
public class EventCancellationRequestDto
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
}
