namespace EventPulse.PaymentService.Events;

public class PaymentSucceededEvent
{
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string StripeSessionId { get; set; } = string.Empty;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
}
