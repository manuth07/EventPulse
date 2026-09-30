namespace EventPulse.BookingService.Events;

public record CancelledTicketItemDto(Guid TicketTypeId, int Quantity);

public class BookingCancelledEvent
{
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid EventId { get; set; }
    public IReadOnlyList<CancelledTicketItemDto> ReleasedTickets { get; set; } = Array.Empty<CancelledTicketItemDto>();
    public DateTimeOffset CancelledAt { get; set; } = DateTimeOffset.UtcNow;

    // Backward-compatibility support for existing event payloads
    public List<BookingItemDto> Items { get; set; } = new();
}
