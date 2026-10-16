namespace EventPulse.Contracts.Kafka.Events;

/// <summary>
/// Represents a released ticket item in a cancellation event.
/// </summary>
public class CancelledTicketItem
{
    public Guid TicketTypeId { get; set; }
    public int Quantity { get; set; }

    public CancelledTicketItem() { }

    public CancelledTicketItem(Guid ticketTypeId, int quantity)
    {
        TicketTypeId = ticketTypeId;
        Quantity = quantity;
    }
}

/// <summary>
/// Represents a booking item snapshot in a cancellation event for backward-compatibility.
/// </summary>
public class BookingCancelledItem
{
    public Guid TicketTypeId { get; set; }
    public string TicketName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

/// <summary>
/// Integration event published when a booking is cancelled (EP-363 / EP-364).
/// Consumed by Event Service to restore ticket availability and capacity idempotently.
/// </summary>
public class BookingCancelledIntegrationEvent
{
    public Guid EventMessageId { get; set; } = Guid.NewGuid();
    public int EventVersion { get; set; } = 1;
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid EventId { get; set; }
    public List<CancelledTicketItem> ReleasedTickets { get; set; } = new();
    public DateTimeOffset CancelledAt { get; set; } = DateTimeOffset.UtcNow;
    public List<BookingCancelledItem> Items { get; set; } = new();
}
