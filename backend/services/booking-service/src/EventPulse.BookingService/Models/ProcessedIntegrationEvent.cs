namespace EventPulse.BookingService.Models;

public class ProcessedIntegrationEvent
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
