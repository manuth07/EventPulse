namespace EventPulse.BookingService.DTOs;

public class CartItemDto
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public string TicketTypeName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int? AvailableQuantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}