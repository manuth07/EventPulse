namespace EventPulse.BookingService.Models;

/// <summary>
/// Represents one ticket-type selection within a specific Cart.
/// Exactly one row per (CartId, TicketTypeId) is enforced.
/// </summary>
public class CartItem
{
    public Guid Id { get; set; }
    public Guid CartId { get; set; }
    public Cart? Cart { get; set; }

    public Guid CustomerId { get; set; }
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }

    /// <summary>Snapshot fields — avoid a live cross-service call every time the cart is displayed.</summary>
    public string TicketTypeName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}