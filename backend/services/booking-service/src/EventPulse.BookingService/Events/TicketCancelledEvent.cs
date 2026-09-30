namespace EventPulse.BookingService.Events;

public class TicketCancelledEvent
{
    public Guid TicketId { get; set; }
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public DateTimeOffset CancelledAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
}
