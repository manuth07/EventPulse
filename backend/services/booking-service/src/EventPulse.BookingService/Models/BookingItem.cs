namespace EventPulse.BookingService.Models;

public class BookingItem
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid TicketTypeId { get; set; }
    public string TicketName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public Booking? Booking { get; set; }
}
