namespace EventPulse.EventService.Models;

public class TicketType
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }

    /// <summary>
    /// Number of tickets currently booked/reserved against this ticket type.
    /// Incremented by the Booking Service (future work) when a booking is confirmed.
    /// Never exceeds Capacity — enforced at the point capacity is changed.
    /// </summary>
    public int BookedQuantity { get; set; } = 0;

    /// <summary>
    /// Total quantity configured for this ticket type. Authoritative total inventory (alias for Capacity).
    /// </summary>
    public int TotalQuantity => Capacity;

    /// <summary>
    /// Sold quantity booked against this ticket type. Authoritative sold inventory (alias for BookedQuantity).
    /// </summary>
    public int SoldQuantity => BookedQuantity;

    /// <summary>
    /// Calculated remaining unsold ticket quantity.
    /// Clamped between 0 and TotalQuantity to satisfy business invariants:
    /// Remaining quantity must never be negative and must never exceed total quantity.
    /// </summary>
    public int RemainingQuantity => Math.Clamp(Capacity - BookedQuantity, 0, Capacity);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}