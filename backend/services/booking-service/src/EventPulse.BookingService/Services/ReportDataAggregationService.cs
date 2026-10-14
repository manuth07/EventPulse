using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

/// <summary>
/// Reusable backend aggregation service for dynamic platform reports (US-39 / EP-377).
/// Implements calculation rules and data aggregation across authoritative bookings and sales reports.
/// </summary>
public class ReportDataAggregationService : IReportDataAggregationService
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventAvailabilityClient _eventAvailabilityClient;
    private readonly ILogger<ReportDataAggregationService> _logger;

    public ReportDataAggregationService(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventAvailabilityClient,
        ILogger<ReportDataAggregationService> logger)
    {
        _dbContext = dbContext;
        _eventAvailabilityClient = eventAvailabilityClient;
        _logger = logger;
    }

    public async Task<BookingsReportDataDto> GetBookingsReportDataAsync(
        BookingsReportQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(query.From, query.To);

        var period = new ReportingPeriodDto { From = query.From, To = query.To };

        // 1. Resolve Organizer filter boundary if specified
        List<Guid>? organizerEventIds = null;
        if (query.OrganizerId.HasValue)
        {
            try
            {
                var events = await _eventAvailabilityClient.GetOrganizerEventsAsync(query.OrganizerId.Value, cancellationToken);
                organizerEventIds = events.Select(e => e.Id).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve events for Organizer {OrganizerId}", query.OrganizerId.Value);
                organizerEventIds = new List<Guid>();
            }

            if (organizerEventIds.Count == 0)
            {
                return new BookingsReportDataDto { ReportingPeriod = period };
            }
        }

        // 2. Build EF query for Bookings
        var bookingsQuery = _dbContext.Bookings
            .Include(b => b.Items)
            .AsNoTracking();

        if (organizerEventIds != null)
        {
            bookingsQuery = bookingsQuery.Where(b => organizerEventIds.Contains(b.EventId));
        }

        if (query.EventId.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(b => b.EventId == query.EventId.Value);
        }

        if (query.From.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(b => (b.ConfirmedAt ?? b.CreatedAt) >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(b => (b.ConfirmedAt ?? b.CreatedAt) <= query.To.Value);
        }

        var allMatchingBookings = await bookingsQuery.ToListAsync(cancellationToken);

        if (allMatchingBookings.Count == 0)
        {
            return new BookingsReportDataDto { ReportingPeriod = period };
        }

        // 3. Status classification following EP-375 rules
        var confirmedBookings = allMatchingBookings
            .Where(b => b.Status == BookingStatus.Confirmed)
            .ToList();

        var cancelledBookings = allMatchingBookings
            .Where(b => b.Status == BookingStatus.Cancelled && (b.ConfirmedAt != null || b.CancelledAt != null))
            .ToList();

        var pendingPaymentBookings = allMatchingBookings
            .Where(b => b.Status == BookingStatus.PendingPayment)
            .ToList();

        var paymentFailedBookings = allMatchingBookings
            .Where(b => b.Status == BookingStatus.PaymentFailed)
            .ToList();

        // Specific status filter override if requested by caller
        if (!string.IsNullOrWhiteSpace(query.BookingStatus) &&
            !query.BookingStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<BookingStatus>(query.BookingStatus, true, out var requestedStatus))
            {
                allMatchingBookings = allMatchingBookings.Where(b => b.Status == requestedStatus).ToList();
            }
        }

        // 4. Compute canonical metrics
        var totalBookings = confirmedBookings.Count + cancelledBookings.Count;
        var activeTickets = confirmedBookings.SelectMany(b => b.Items).Sum(i => i.Quantity);
        var cancelledTickets = cancelledBookings.SelectMany(b => b.Items).Sum(i => i.Quantity);
        var grossTickets = activeTickets + cancelledTickets;

        var cancellationRate = totalBookings > 0
            ? Math.Round(((decimal)cancelledBookings.Count / totalBookings) * 100m, 2)
            : 0.0m;

        var ticketCancellationRate = grossTickets > 0
            ? Math.Round(((decimal)cancelledTickets / grossTickets) * 100m, 2)
            : 0.0m;

        var averageTicketsPerBooking = confirmedBookings.Count > 0
            ? Math.Round((decimal)activeTickets / confirmedBookings.Count, 2)
            : 0.0m;

        // 5. Compute per-event breakdown
        var completedBookings = confirmedBookings.Concat(cancelledBookings).ToList();
        var eventGroups = completedBookings.GroupBy(b => b.EventId);
        var eventBreakdown = new List<EventBookingAggregationItemDto>();

        foreach (var group in eventGroups)
        {
            var eventConfirmed = group.Count(b => b.Status == BookingStatus.Confirmed);
            var eventCancelled = group.Count(b => b.Status == BookingStatus.Cancelled);
            var eventTotal = eventConfirmed + eventCancelled;

            var eventActiveTickets = group.Where(b => b.Status == BookingStatus.Confirmed)
                .SelectMany(b => b.Items).Sum(i => i.Quantity);
            var eventCancelledTickets = group.Where(b => b.Status == BookingStatus.Cancelled)
                .SelectMany(b => b.Items).Sum(i => i.Quantity);

            var eventCancRate = eventTotal > 0
                ? Math.Round(((decimal)eventCancelled / eventTotal) * 100m, 2)
                : 0.0m;

            eventBreakdown.Add(new EventBookingAggregationItemDto
            {
                EventId = group.Key,
                TotalBookings = eventTotal,
                ConfirmedBookings = eventConfirmed,
                CancelledBookings = eventCancelled,
                ActiveTickets = eventActiveTickets,
                CancelledTickets = eventCancelledTickets,
                GrossTickets = eventActiveTickets + eventCancelledTickets,
                CancellationRate = eventCancRate
            });
        }

        // 6. Compute per-ticket-type tier breakdown
        var ticketTypeBreakdown = completedBookings
            .SelectMany(b => b.Items.Select(item => new { Booking = b, Item = item }))
            .GroupBy(x => new { x.Item.TicketTypeId, x.Item.TicketName })
            .Select(g =>
            {
                var activeQty = g.Where(x => x.Booking.Status == BookingStatus.Confirmed).Sum(x => x.Item.Quantity);
                var cancQty = g.Where(x => x.Booking.Status == BookingStatus.Cancelled).Sum(x => x.Item.Quantity);
                var subtotal = g.Where(x => x.Booking.Status == BookingStatus.Confirmed).Sum(x => x.Item.Subtotal);
                var unitPrice = g.Select(x => x.Item.UnitPrice).FirstOrDefault();

                return new TicketTypeAggregationItemDto
                {
                    TicketTypeId = g.Key.TicketTypeId,
                    TicketName = g.Key.TicketName,
                    TotalQuantity = activeQty + cancQty,
                    ActiveQuantity = activeQty,
                    CancelledQuantity = cancQty,
                    UnitPrice = unitPrice,
                    TotalSubtotal = subtotal
                };
            })
            .OrderByDescending(t => t.TotalQuantity)
            .ToList();

        return new BookingsReportDataDto
        {
            TotalBookings = totalBookings,
            ConfirmedBookings = confirmedBookings.Count,
            CancelledBookings = cancelledBookings.Count,
            PendingPaymentBookings = pendingPaymentBookings.Count,
            PaymentFailedBookings = paymentFailedBookings.Count,
            GrossTicketsSold = grossTickets,
            ActiveTicketsBooked = activeTickets,
            CancelledTickets = cancelledTickets,
            CancellationRate = cancellationRate,
            TicketCancellationRate = ticketCancellationRate,
            AverageTicketsPerBooking = averageTicketsPerBooking,
            ReportingPeriod = period,
            EventsBreakdown = eventBreakdown.OrderByDescending(e => e.TotalBookings).ToList(),
            TicketTypesBreakdown = ticketTypeBreakdown
        };
    }

    public async Task<RevenueReportDataDto> GetRevenueReportDataAsync(
        RevenueReportQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(query.From, query.To);

        var period = new ReportingPeriodDto { From = query.From, To = query.To };

        var salesQuery = _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .AsNoTracking();

        if (query.EventId.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.EventId == query.EventId.Value);
        }

        if (query.OrganizerId.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.OrganizerId == query.OrganizerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            var normalizedCurrency = query.Currency.Trim().ToUpperInvariant();
            salesQuery = salesQuery.Where(s => s.Currency.ToUpper() == normalizedCurrency);
        }

        if (query.From.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.PaymentTimestampUtc >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.PaymentTimestampUtc <= query.To.Value);
        }

        var reports = await salesQuery.ToListAsync(cancellationToken);

        if (reports.Count == 0)
        {
            return new RevenueReportDataDto
            {
                Currency = !string.IsNullOrWhiteSpace(query.Currency) ? query.Currency.ToUpperInvariant() : "USD",
                ReportingPeriod = period
            };
        }

        // Multi-currency strict segregation (EP-375 Rule 6.5)
        var currencyGroups = reports
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Currency) ? "USD" : r.Currency.ToUpperInvariant())
            .Select(g =>
            {
                var gross = g.Sum(r => r.SaleAmount);
                var refunded = g.Sum(r => r.RefundedAmount);
                var net = Math.Max(0m, gross - refunded);
                var purchaseCount = g.Count();
                var aov = purchaseCount > 0 ? Math.Round(gross / purchaseCount, 2) : 0.0m;
                var refundRate = gross > 0 ? Math.Round((refunded / gross) * 100m, 2) : 0.0m;
                var ticketsSold = g.SelectMany(r => r.Items).Sum(i => i.Quantity);
                var netTicketsSold = g.SelectMany(r => r.Items).Sum(i => i.Quantity - i.RefundedQuantity);

                return new CurrencyRevenueSummaryDto
                {
                    Currency = g.Key,
                    GrossRevenue = gross,
                    RefundedAmount = refunded,
                    NetRevenue = net,
                    SuccessfulPurchaseCount = purchaseCount,
                    AverageOrderValue = aov,
                    RefundRate = refundRate,
                    TicketsSold = ticketsSold,
                    NetTicketsSold = Math.Max(0, netTicketsSold)
                };
            })
            .OrderByDescending(c => c.GrossRevenue)
            .ToList();

        // Primary currency summary
        var primaryCurrencySummary = currencyGroups.First();
        var fullRefunds = reports.Count(r => r.RefundStatus == SalesRefundStatus.Refunded);
        var partialRefunds = reports.Count(r => r.RefundStatus == SalesRefundStatus.PartiallyRefunded);

        // Per-event revenue breakdown
        var eventBreakdown = reports
            .GroupBy(r => new { r.EventId, Currency = string.IsNullOrWhiteSpace(r.Currency) ? "USD" : r.Currency.ToUpperInvariant() })
            .Select(g =>
            {
                var gross = g.Sum(r => r.SaleAmount);
                var refunded = g.Sum(r => r.RefundedAmount);
                return new EventRevenueAggregationItemDto
                {
                    EventId = g.Key.EventId,
                    Currency = g.Key.Currency,
                    GrossRevenue = gross,
                    RefundedAmount = refunded,
                    NetRevenue = Math.Max(0m, gross - refunded),
                    PurchaseCount = g.Count()
                };
            })
            .OrderByDescending(e => e.GrossRevenue)
            .ToList();

        // Per-organizer revenue breakdown
        var organizerBreakdown = reports
            .GroupBy(r => new { r.OrganizerId, Currency = string.IsNullOrWhiteSpace(r.Currency) ? "USD" : r.Currency.ToUpperInvariant() })
            .Select(g =>
            {
                var gross = g.Sum(r => r.SaleAmount);
                var refunded = g.Sum(r => r.RefundedAmount);
                return new OrganizerRevenueAggregationItemDto
                {
                    OrganizerId = g.Key.OrganizerId,
                    Currency = g.Key.Currency,
                    GrossRevenue = gross,
                    RefundedAmount = refunded,
                    NetRevenue = Math.Max(0m, gross - refunded),
                    PurchaseCount = g.Count()
                };
            })
            .OrderByDescending(o => o.GrossRevenue)
            .ToList();

        return new RevenueReportDataDto
        {
            GrossRevenue = primaryCurrencySummary.GrossRevenue,
            RefundedAmount = primaryCurrencySummary.RefundedAmount,
            NetRevenue = primaryCurrencySummary.NetRevenue,
            SuccessfulPurchaseCount = primaryCurrencySummary.SuccessfulPurchaseCount,
            AverageOrderValue = primaryCurrencySummary.AverageOrderValue,
            RefundRate = primaryCurrencySummary.RefundRate,
            FullRefundCount = fullRefunds,
            PartialRefundCount = partialRefunds,
            Currency = primaryCurrencySummary.Currency,
            ReportingPeriod = period,
            CurrencyBreakdown = currencyGroups,
            EventsBreakdown = eventBreakdown,
            OrganizersBreakdown = organizerBreakdown
        };
    }

    public async Task<PlatformActivityReportDataDto> GetPlatformActivityReportDataAsync(
        PlatformActivityReportQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(query.From, query.To);

        var period = new ReportingPeriodDto { From = query.From, To = query.To };

        // 1. Compute booking throughput
        var bookingsQuery = new BookingsReportQueryParameters
        {
            From = query.From,
            To = query.To,
            EventId = query.EventId,
            OrganizerId = query.OrganizerId
        };
        var bookingsData = await GetBookingsReportDataAsync(bookingsQuery, cancellationToken);

        // 2. Compute revenue financial overview
        var revenueQuery = new RevenueReportQueryParameters
        {
            From = query.From,
            To = query.To,
            EventId = query.EventId,
            OrganizerId = query.OrganizerId
        };
        var revenueData = await GetRevenueReportDataAsync(revenueQuery, cancellationToken);

        // 3. Compute unique active customer and event counts across authoritative bookings
        var bQuery = _dbContext.Bookings.AsNoTracking();
        if (query.EventId.HasValue)
            bQuery = bQuery.Where(b => b.EventId == query.EventId.Value);

        if (query.From.HasValue)
            bQuery = bQuery.Where(b => (b.ConfirmedAt ?? b.CreatedAt) >= query.From.Value);

        if (query.To.HasValue)
            bQuery = bQuery.Where(b => (b.ConfirmedAt ?? b.CreatedAt) <= query.To.Value);

        var uniqueCustomers = await bQuery
            .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Cancelled)
            .Select(b => b.CustomerId)
            .Distinct()
            .CountAsync(cancellationToken);

        var activeEvents = await bQuery
            .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Cancelled)
            .Select(b => b.EventId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new PlatformActivityReportDataDto
        {
            TotalBookings = bookingsData.TotalBookings,
            ConfirmedBookings = bookingsData.ConfirmedBookings,
            CancelledBookings = bookingsData.CancelledBookings,
            CancellationRate = bookingsData.CancellationRate,
            GrossTicketsSold = bookingsData.GrossTicketsSold,
            ActiveTicketsBooked = bookingsData.ActiveTicketsBooked,
            CancelledTickets = bookingsData.CancelledTickets,
            TicketCancellationRate = bookingsData.TicketCancellationRate,
            AverageTicketsPerBooking = bookingsData.AverageTicketsPerBooking,
            PendingPaymentHolds = bookingsData.PendingPaymentBookings,
            PaymentFailedCheckouts = bookingsData.PaymentFailedBookings,

            PrimaryCurrency = revenueData.Currency,
            PrimaryGrossRevenue = revenueData.GrossRevenue,
            PrimaryRefundedAmount = revenueData.RefundedAmount,
            PrimaryNetRevenue = revenueData.NetRevenue,
            TotalSuccessfulPurchases = revenueData.SuccessfulPurchaseCount,
            TotalFullRefunds = revenueData.FullRefundCount,
            TotalPartialRefunds = revenueData.PartialRefundCount,
            CurrencyBreakdown = revenueData.CurrencyBreakdown,

            TotalUniqueCustomers = uniqueCustomers,
            TotalActiveEvents = activeEvents,
            ReportingPeriod = period
        };
    }

    private static void ValidateDateRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ArgumentException("The start date must be earlier than or equal to the end date.");
        }
    }
}
