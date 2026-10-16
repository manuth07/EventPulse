namespace EventPulse.PaymentService.DTOs;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public string Status { get; set; } = string.Empty;
    public string? StripeSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
