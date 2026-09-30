namespace EventPulse.PaymentService.Models;

public class RefundRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    public Guid BookingId { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Payment Payment { get; set; } = null!;
}
