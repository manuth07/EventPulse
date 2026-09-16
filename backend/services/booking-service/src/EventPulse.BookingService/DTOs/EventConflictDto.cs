namespace EventPulse.BookingService.DTOs;

public class EventConflictDto
{
    public string Code { get; set; } = "EVENT_CONFLICT";
    public string Message { get; set; } = string.Empty;
    public Guid CurrentEventId { get; set; }
    public string CurrentEventTitle { get; set; } = string.Empty;
    public Guid AttemptedEventId { get; set; }
}
