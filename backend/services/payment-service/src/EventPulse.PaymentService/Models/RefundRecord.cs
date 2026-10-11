namespace EventPulse.PaymentService.Models;

public class RefundRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public Guid BookingId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "usd";

    public RefundStatus Status { get; set; } = RefundStatus.Completed;

    public string Reason { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public string? StripeRefundId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public Payment Payment { get; set; } = null!;
}
