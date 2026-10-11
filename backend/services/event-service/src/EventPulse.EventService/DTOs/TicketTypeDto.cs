namespace EventPulse.EventService.DTOs;

public class TicketTypeDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }

    private int? _totalQuantity;
    /// <summary>
    /// Total quantity configured for this ticket type. Authoritative total inventory.
    /// </summary>
    public int TotalQuantity
    {
        get => _totalQuantity ?? Capacity;
        set => _totalQuantity = value;
    }

    public int BookedQuantity { get; set; }

    private int? _soldQuantity;
    /// <summary>
    /// Sold quantity booked against this ticket type. Authoritative sold inventory.
    /// </summary>
    public int SoldQuantity
    {
        get => _soldQuantity ?? BookedQuantity;
        set => _soldQuantity = value;
    }

    private int? _remainingQuantity;
    /// <summary>
    /// Calculated remaining unsold ticket quantity.
    /// Clamped between 0 and TotalQuantity to ensure it is never negative and never exceeds total quantity.
    /// </summary>
    public int RemainingQuantity
    {
        get => _remainingQuantity ?? Math.Clamp(Capacity - BookedQuantity, 0, Capacity);
        set => _remainingQuantity = value;
    }

    private int? _availableQuantity;
    /// <summary>
    /// Available tickets for purchase. Preserves backward compatibility by mapping to RemainingQuantity.
    /// </summary>
    public int AvailableQuantity
    {
        get => _availableQuantity ?? RemainingQuantity;
        set => _availableQuantity = value;
    }

    public DateTime CreatedAt { get; set; }
}