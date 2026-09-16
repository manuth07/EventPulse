namespace EventPulse.BookingService.Models;

public enum CartStatus
{
    Active = 1,
    Completed = 2,
    Abandoned = 3
}

/// <summary>
/// Represents a customer's active or historical purchasing cart session.
/// A customer has at most one Active cart at any time, and an active cart
/// belongs strictly to one Event.
/// </summary>
public class Cart
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid EventId { get; set; }
    public CartStatus Status { get; set; } = CartStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
