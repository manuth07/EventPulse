namespace EventPulse.BookingService.Events;

public class BookingCancelledEvent
{
    public Guid BookingId { get; set; }
    public Guid EventId { get; set; }
    public List<BookingItemDto> Items { get; set; } = new();
}
