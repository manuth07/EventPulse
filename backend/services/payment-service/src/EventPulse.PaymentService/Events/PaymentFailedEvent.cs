namespace EventPulse.PaymentService.Events;

public class PaymentFailedEvent
{
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public DateTimeOffset FailedAt { get; set; }
}
