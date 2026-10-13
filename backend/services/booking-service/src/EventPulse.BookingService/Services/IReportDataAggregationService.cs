using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

/// <summary>
/// Reusable backend aggregation service for dynamic platform reports (US-39 / EP-377).
/// Computes canonical metrics for Bookings Report, Revenue Report, and Platform Activity Summary.
/// </summary>
public interface IReportDataAggregationService
{
    /// <summary>
    /// Aggregates operational booking metrics, ticket counts, cancellation rates,
    /// and per-event/ticket-tier breakdowns across authoritative bookings.
    /// </summary>
    Task<BookingsReportDataDto> GetBookingsReportDataAsync(
        BookingsReportQueryParameters query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates financial sales metrics, refunds, net proceeds, and multi-currency
    /// breakdowns across authoritative sales reports.
    /// </summary>
    Task<RevenueReportDataDto> GetRevenueReportDataAsync(
        RevenueReportQueryParameters query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates holistic platform activity throughput including bookings, revenue,
    /// customer counts, and event participation.
    /// </summary>
    Task<PlatformActivityReportDataDto> GetPlatformActivityReportDataAsync(
        PlatformActivityReportQueryParameters query,
        CancellationToken cancellationToken = default);
}
