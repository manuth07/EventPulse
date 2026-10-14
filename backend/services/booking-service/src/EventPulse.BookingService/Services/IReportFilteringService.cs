using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

/// <summary>
/// Generalized filter criteria for dynamic reports and analytics (EP-394 / US-42).
/// Supports date range, event, booking status, ticket type, category, currency, and organizer filters.
/// </summary>
public class ReportFilterCriteria
{
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Guid? EventId { get; set; }
    public Guid? OrganizerId { get; set; }
    public string? BookingStatus { get; set; }
    public Guid? TicketTypeId { get; set; }
    public string? Category { get; set; }
    public string? Currency { get; set; }
}

/// <summary>
/// Reusable backend filtering service for platform reports and analytics queries.
/// Supports multi-attribute filtering using strict boolean AND logic, cross-service category resolution,
/// and line-item slicing for ticket type queries.
/// </summary>
public interface IReportFilteringService
{
    /// <summary>
    /// Resolves the subset of Event IDs matching organizer, category, and eventId constraints.
    /// Returns null if no event constraints are applied.
    /// Returns an empty list if constraints produce no matches (contradiction / zero results).
    /// </summary>
    Task<IReadOnlyList<Guid>?> ResolveAllowedEventIdsAsync(
        Guid? organizerId,
        string? category,
        Guid? eventId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies criteria and allowed event IDs to an EF Core Bookings query.
    /// </summary>
    IQueryable<Booking> ApplyBookingFilters(
        IQueryable<Booking> query,
        ReportFilterCriteria criteria,
        IReadOnlyList<Guid>? allowedEventIds);

    /// <summary>
    /// Applies criteria and allowed event IDs to an EF Core SalesReports query.
    /// </summary>
    IQueryable<SalesReport> ApplySalesReportFilters(
        IQueryable<SalesReport> query,
        ReportFilterCriteria criteria,
        IReadOnlyList<Guid>? allowedEventIds);

    /// <summary>
    /// Returns the booking items that match the optional ticket type filter.
    /// </summary>
    IReadOnlyList<BookingItem> GetMatchingBookingItems(Booking booking, Guid? ticketTypeId = null);

    /// <summary>
    /// Sums tickets in a booking matching the optional ticket type filter.
    /// </summary>
    int GetFilteredBookingTickets(Booking booking, Guid? ticketTypeId = null);

    /// <summary>
    /// Returns sales report line items matching the optional ticket type filter.
    /// </summary>
    IReadOnlyList<SalesReportItem> GetMatchingSalesItems(SalesReport report, Guid? ticketTypeId = null);

    /// <summary>
    /// Calculates gross revenue for matching line items of a sales report.
    /// </summary>
    decimal GetFilteredGrossRevenue(SalesReport report, Guid? ticketTypeId = null);

    /// <summary>
    /// Calculates refunded amount for matching line items of a sales report.
    /// </summary>
    decimal GetFilteredRefundedAmount(SalesReport report, Guid? ticketTypeId = null);

    /// <summary>
    /// Calculates net revenue for matching line items of a sales report.
    /// </summary>
    decimal GetFilteredNetRevenue(SalesReport report, Guid? ticketTypeId = null);

    /// <summary>
    /// Calculates total tickets sold for matching line items of a sales report.
    /// </summary>
    int GetFilteredTicketsSold(SalesReport report, Guid? ticketTypeId = null);

    /// <summary>
    /// Calculates net tickets sold for matching line items of a sales report.
    /// </summary>
    int GetFilteredNetTicketsSold(SalesReport report, Guid? ticketTypeId = null);
}
