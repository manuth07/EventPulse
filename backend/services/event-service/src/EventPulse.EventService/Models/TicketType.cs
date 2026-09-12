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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}