namespace EventPulse.EventService.Models;

/// <summary>
/// EP-401: Tracks inventory restorations from cancelled bookings/tickets for idempotency.
/// Prevents duplicate Kafka/HTTP cancellation events from double-restoring inventory.
/// </summary>
public class InventoryRestoration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid BookingId { get; set; }
    public Guid? TicketId { get; set; }
    public string CancellationKey { get; set; } = string.Empty;
    public Guid TicketTypeId { get; set; }
    public int RestoredQuantity { get; set; }
    public DateTimeOffset RestoredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public TicketType? TicketType { get; set; }
}
