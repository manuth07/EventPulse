namespace EventPulse.EventService.Models;

public enum InventoryReservationStatus
{
    Active = 1,
    Committed = 2,
    Released = 3
}

/// <summary>
/// EP-401: Tracks temporary inventory holds for pending checkouts to prevent overselling.
/// Distinguishes temporarily reserved quantity from completed sales.
/// </summary>
public class InventoryReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid BookingId { get; set; }
    public Guid TicketTypeId { get; set; }
    public int Quantity { get; set; }
    public InventoryReservationStatus Status { get; set; } = InventoryReservationStatus.Active;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }

    public TicketType? TicketType { get; set; }
}
