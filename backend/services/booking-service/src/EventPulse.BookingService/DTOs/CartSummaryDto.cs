namespace EventPulse.BookingService.DTOs;

public class CartSummaryDto
{
    public IReadOnlyList<CartItemDto> Items { get; set; } = new List<CartItemDto>();
    public decimal TotalAmount => Items.Sum(i => i.LineTotal);
    public int TotalTicketCount => Items.Sum(i => i.Quantity);
}