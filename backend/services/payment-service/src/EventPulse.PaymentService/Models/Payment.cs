namespace EventPulse.PaymentService.Models;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }

    public string BookingReference { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public string? StripeSessionId { get; set; }

    public string? StripePaymentIntentId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "usd";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }
}
