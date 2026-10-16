namespace EventPulse.BookingService.Models;

public class SaleRefundAdjustment
{
    public Guid Id { get; set; }

    public Guid SalesReportId { get; set; }

    /// <summary>
    /// Unique refund identifier from Payment Service / Stripe.
    /// Ensures idempotent refund application without duplicate accounting.
    /// </summary>
    public Guid RefundId { get; set; }

    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = "Completed";

    public DateTimeOffset AdjustedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public SalesReport? SalesReport { get; set; }
}
