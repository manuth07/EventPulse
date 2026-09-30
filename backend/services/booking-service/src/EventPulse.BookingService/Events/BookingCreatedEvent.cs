namespace EventPulse.BookingService.Events;

public class BookingItemDto
{
    public Guid TicketTypeId { get; set; }
    public string TicketName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public class BookingCreatedEvent
{
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid EventId { get; set; }
    public decimal TotalAmount { get; set; }
    public List<BookingItemDto> Items { get; set; } = new List<BookingItemDto>();
    public DateTimeOffset CreatedAt { get; set; }
}
