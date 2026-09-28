namespace EventPulse.Contracts.Kafka;

public class PaymentSucceededEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public int EventVersion { get; set; } = 1;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}
