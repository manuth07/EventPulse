namespace EventPulse.BookingService.Models;

public enum SalesRefundStatus
{
    None = 0,
    Pending = 1,
    PartiallyRefunded = 2,
    Refunded = 3
}

public class SalesReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique payment reference from Payment Service / Stripe (PaymentId).
    /// Enforces idempotency to avoid double counting.
    /// </summary>
    public Guid PaymentId { get; set; }

    /// <summary>
    /// Associated Booking identifier.
    /// Exactly one confirmed sale record per booking.
    /// </summary>
    public Guid BookingId { get; set; }

    public string BookingReference { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid EventId { get; set; }

    public Guid OrganizerId { get; set; }

    /// <summary>
    /// Total gross amount charged for this sale.
    /// </summary>
    public decimal SaleAmount { get; set; }

    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Authoritative timestamp when payment succeeded.
    /// </summary>
    public DateTimeOffset PaymentTimestampUtc { get; set; }

    /// <summary>
    /// Cumulative refunded amount across any completed refunds.
    /// </summary>
    public decimal RefundedAmount { get; set; } = 0m;

    public SalesRefundStatus RefundStatus { get; set; } = SalesRefundStatus.None;

    public DateTimeOffset? RefundedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Net sale amount (Gross SaleAmount - RefundedAmount).
    /// </summary>
    public decimal NetSaleAmount => Math.Max(0m, SaleAmount - RefundedAmount);

    /// <summary>
    /// Line items preserving ticket type breakdowns.
    /// </summary>
    public ICollection<SalesReportItem> Items { get; set; } = new List<SalesReportItem>();

    /// <summary>
    /// History of applied refund adjustments.
    /// </summary>
    public ICollection<SaleRefundAdjustment> RefundAdjustments { get; set; } = new List<SaleRefundAdjustment>();
}
