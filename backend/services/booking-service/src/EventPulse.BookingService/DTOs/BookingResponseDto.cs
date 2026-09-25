namespace EventPulse.BookingService.DTOs;

public class BookingItemResponseDto
{
    public Guid TicketTypeId { get; set; }
    public string TicketName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public class BookingResponseDto
{
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<BookingItemResponseDto> Items { get; set; } = new();
}
