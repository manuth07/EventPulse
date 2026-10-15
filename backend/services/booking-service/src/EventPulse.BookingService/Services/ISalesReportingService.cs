using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.Contracts.Kafka;

namespace EventPulse.BookingService.Services;

public interface ISalesReportingService
{
    /// <summary>
    /// EP-383: Authoritatively records a successful purchase into the sales reporting read-model.
    /// Strictly idempotent: duplicate webhooks, duplicate Kafka events, or multiple payment attempts
    /// will not duplicate sales records or double-count revenue.
    /// </summary>
    Task<SalesReport> GenerateSalesReportOnPurchaseAsync(PaymentSucceededEvent paymentEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-383: Applies a refund adjustment to an existing sales report record without deleting historical sale data.
    /// Supports partial and full refunds, updating refunded amounts, status, and line items.
    /// Idempotent by refundId.
    /// </summary>
    Task<SalesReport?> ApplyRefundAdjustmentAsync(
        Guid bookingId,
        Guid refundId,
        decimal refundAmount,
        string refundStatus,
        string? reason = null,
        DateTimeOffset? refundedAtUtc = null,
        CancellationToken cancellationToken = default);

    Task<SalesReport?> GetSalesReportByIdAsync(Guid saleId, CancellationToken cancellationToken = default);

    Task<SalesReport?> GetSalesReportByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<SalesReport?> GetSalesReportByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesReport>> GetSalesReportsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesReport>> GetSalesReportsByOrganizerIdAsync(Guid organizerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-405: Retrieves a paginated collection of sales reports with server-side filtering and sorting.
    /// </summary>
    Task<PagedResult<SalesReportDto>> GetSalesReportsAsync(ReportFilterDto filter, CancellationToken cancellationToken = default);
}
