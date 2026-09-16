namespace EventPulse.BookingService.DTOs;

public class CartSummaryDto
{
    public Guid? CartId { get; set; }
    public Guid? EventId { get; set; }
    public string? EventTitle { get; set; }
    public string? EventVenue { get; set; }
    public DateTime? EventDate { get; set; }
    public string? EventImageUrl { get; set; }

    public IReadOnlyList<CartItemDto> Items { get; set; } = new List<CartItemDto>();
    public decimal TotalAmount => Items.Sum(i => i.LineTotal);
    public int TotalTicketCount => Items.Sum(i => i.Quantity);
}