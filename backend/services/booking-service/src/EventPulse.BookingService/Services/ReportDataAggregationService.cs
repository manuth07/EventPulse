using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPulse.BookingService.Services;

/// <summary>
/// Reusable backend aggregation service for dynamic platform reports (US-39 / EP-377).
/// Implements calculation rules and data aggregation across authoritative bookings and sales reports.
/// Integrated with IReportFilteringService (EP-394) for multi-dimensional filtering and line-item slicing.
/// </summary>
public class ReportDataAggregationService : IReportDataAggregationService
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventAvailabilityClient _eventAvailabilityClient;
    private readonly IReportFilteringService _filteringService;
    private readonly ILogger<ReportDataAggregationService> _logger;

    public ReportDataAggregationService(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventAvailabilityClient,
        ILogger<ReportDataAggregationService> logger)
        : this(
            dbContext,
            eventAvailabilityClient,
            new ReportFilteringService(dbContext, eventAvailabilityClient, NullLogger<ReportFilteringService>.Instance),
            logger)
    {
    }

    public ReportDataAggregationService(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventAvailabilityClient,
        IReportFilteringService filteringService,
        ILogger<ReportDataAggregationService> logger)
    {
        _dbContext = dbContext;
        _eventAvailabilityClient = eventAvailabilityClient;
        _filteringService = filteringService;
        _logger = logger;
    }

    public async Task<BookingsReportDataDto> GetBookingsReportDataAsync(
        BookingsReportQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(query.From, query.To);

        var period = new ReportingPeriodDto { From = query.From, To = query.To };

        // 1. Resolve event boundaries across organizer, category, and event constraints
        var allowedEventIds = await _filteringService.ResolveAllowedEventIdsAsync(
            query.OrganizerId,
            query.Category,
            query.EventId,
            cancellationToken);

        if (allowedEventIds != null && allowedEventIds.Count == 0)
        {
            return new BookingsReportDataDto { ReportingPeriod = period };
        }

        // 2. Build EF query for Bookings
        var bookingsQuery = _dbContext.Bookings
            .Include(b => b.Items)
            .AsNoTracking();

        var criteria = new ReportFilterCriteria
        {
            From = query.From,
            To = query.To,
            EventId = query.EventId,
            BookingStatus = query.BookingStatus,
            TicketTypeId = query.TicketTypeId,
            Category = query.Category,
            OrganizerId = query.OrganizerId
        };

        bookingsQuery = _filteringService.ApplyBookingFilters(bookingsQuery, criteria, allowedEventIds);

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

        // 4. Compute canonical metrics with line-item slicing support
        var isSpecificStatusFiltered = !string.IsNullOrWhiteSpace(query.BookingStatus) &&
            !query.BookingStatus.Equals("All", StringComparison.OrdinalIgnoreCase);

        var totalBookings = isSpecificStatusFiltered
            ? allMatchingBookings.Count
            : confirmedBookings.Count + cancelledBookings.Count;

        var activeTickets = confirmedBookings.Sum(b => _filteringService.GetFilteredBookingTickets(b, query.TicketTypeId));
        var cancelledTickets = cancelledBookings.Sum(b => _filteringService.GetFilteredBookingTickets(b, query.TicketTypeId));
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
        var completedBookings = isSpecificStatusFiltered
            ? allMatchingBookings
            : confirmedBookings.Concat(cancelledBookings).ToList();

        var eventGroups = completedBookings.GroupBy(b => b.EventId);
        var eventBreakdown = new List<EventBookingAggregationItemDto>();

        foreach (var group in eventGroups)
        {
            var eventConfirmed = group.Count(b => b.Status == BookingStatus.Confirmed);
            var eventCancelled = group.Count(b => b.Status == BookingStatus.Cancelled);
            var eventTotal = isSpecificStatusFiltered ? group.Count() : eventConfirmed + eventCancelled;

            var eventActiveTickets = group.Where(b => b.Status == BookingStatus.Confirmed)
                .Sum(b => _filteringService.GetFilteredBookingTickets(b, query.TicketTypeId));
            var eventCancelledTickets = group.Where(b => b.Status == BookingStatus.Cancelled)
                .Sum(b => _filteringService.GetFilteredBookingTickets(b, query.TicketTypeId));

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
            .SelectMany(b => _filteringService.GetMatchingBookingItems(b, query.TicketTypeId).Select(item => new { Booking = b, Item = item }))
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

        // 1. Resolve event boundaries across organizer, category, and event constraints
        var allowedEventIds = await _filteringService.ResolveAllowedEventIdsAsync(
            query.OrganizerId,
            query.Category,
            query.EventId,
            cancellationToken);

        if (allowedEventIds != null && allowedEventIds.Count == 0)
        {
            return new RevenueReportDataDto
            {
                Currency = !string.IsNullOrWhiteSpace(query.Currency) ? query.Currency.ToUpperInvariant() : "USD",
                ReportingPeriod = period
            };
        }

        // 2. Build EF query for SalesReports
        var salesQuery = _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .AsNoTracking();

        var criteria = new ReportFilterCriteria
        {
            From = query.From,
            To = query.To,
            EventId = query.EventId,
            OrganizerId = query.OrganizerId,
            Currency = query.Currency,
            BookingStatus = query.BookingStatus,
            TicketTypeId = query.TicketTypeId,
            Category = query.Category
        };

        salesQuery = _filteringService.ApplySalesReportFilters(salesQuery, criteria, allowedEventIds);

        // Status-based sales filter (EP-393 Section 8.1 Option B)
        if (!string.IsNullOrWhiteSpace(query.BookingStatus) &&
            !query.BookingStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (query.BookingStatus.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                salesQuery = salesQuery.Where(s => s.RefundStatus != SalesRefundStatus.Refunded);
            }
            else if (query.BookingStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                salesQuery = salesQuery.Where(s => s.RefundStatus == SalesRefundStatus.Refunded);
            }
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

        // 3. Multi-currency strict segregation (EP-375 Rule 6.5) with line-item slicing
        var currencyGroups = reports
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Currency) ? "USD" : r.Currency.ToUpperInvariant())
            .Select(g =>
            {
                var gross = g.Sum(r => _filteringService.GetFilteredGrossRevenue(r, query.TicketTypeId));
                var refunded = g.Sum(r => _filteringService.GetFilteredRefundedAmount(r, query.TicketTypeId));
                var net = Math.Max(0m, gross - refunded);
                var purchaseCount = g.Count();
                var aov = purchaseCount > 0 ? Math.Round(gross / purchaseCount, 2) : 0.0m;
                var refundRate = gross > 0 ? Math.Round((refunded / gross) * 100m, 2) : 0.0m;
                var ticketsSold = g.Sum(r => _filteringService.GetFilteredTicketsSold(r, query.TicketTypeId));
                var netTicketsSold = g.Sum(r => _filteringService.GetFilteredNetTicketsSold(r, query.TicketTypeId));

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

        int fullRefunds;
        int partialRefunds;
        if (query.TicketTypeId.HasValue)
        {
            fullRefunds = reports.Count(r =>
            {
                var gross = _filteringService.GetFilteredGrossRevenue(r, query.TicketTypeId);
                var refAmt = _filteringService.GetFilteredRefundedAmount(r, query.TicketTypeId);
                return gross > 0 && refAmt >= gross;
            });

            partialRefunds = reports.Count(r =>
            {
                var gross = _filteringService.GetFilteredGrossRevenue(r, query.TicketTypeId);
                var refAmt = _filteringService.GetFilteredRefundedAmount(r, query.TicketTypeId);
                return gross > 0 && refAmt > 0 && refAmt < gross;
            });
        }
        else
        {
            fullRefunds = reports.Count(r => r.RefundStatus == SalesRefundStatus.Refunded);
            partialRefunds = reports.Count(r => r.RefundStatus == SalesRefundStatus.PartiallyRefunded);
        }

        // Per-event revenue breakdown with line-item slicing
        var eventBreakdown = reports
            .GroupBy(r => new { r.EventId, Currency = string.IsNullOrWhiteSpace(r.Currency) ? "USD" : r.Currency.ToUpperInvariant() })
            .Select(g =>
            {
                var gross = g.Sum(r => _filteringService.GetFilteredGrossRevenue(r, query.TicketTypeId));
                var refunded = g.Sum(r => _filteringService.GetFilteredRefundedAmount(r, query.TicketTypeId));
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

        // Per-organizer revenue breakdown with line-item slicing
        var organizerBreakdown = reports
            .GroupBy(r => new { r.OrganizerId, Currency = string.IsNullOrWhiteSpace(r.Currency) ? "USD" : r.Currency.ToUpperInvariant() })
            .Select(g =>
            {
                var gross = g.Sum(r => _filteringService.GetFilteredGrossRevenue(r, query.TicketTypeId));
                var refunded = g.Sum(r => _filteringService.GetFilteredRefundedAmount(r, query.TicketTypeId));
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
            OrganizerId = query.OrganizerId,
            BookingStatus = query.BookingStatus,
            TicketTypeId = query.TicketTypeId,
            Category = query.Category
        };
        var bookingsData = await GetBookingsReportDataAsync(bookingsQuery, cancellationToken);

        // 2. Compute revenue financial overview
        var revenueQuery = new RevenueReportQueryParameters
        {
            From = query.From,
            To = query.To,
            EventId = query.EventId,
            OrganizerId = query.OrganizerId,
            Currency = query.Currency,
            TicketTypeId = query.TicketTypeId,
            Category = query.Category,
            BookingStatus = query.BookingStatus
        };
        var revenueData = await GetRevenueReportDataAsync(revenueQuery, cancellationToken);

        // 3. Compute unique active customer and event counts across authoritative bookings
        var allowedEventIds = await _filteringService.ResolveAllowedEventIdsAsync(
            query.OrganizerId,
            query.Category,
            query.EventId,
            cancellationToken);

        int uniqueCustomers = 0;
        int activeEvents = 0;

        if (allowedEventIds == null || allowedEventIds.Count > 0)
        {
            var bQuery = _dbContext.Bookings.AsNoTracking();

            bQuery = _filteringService.ApplyBookingFilters(bQuery, new ReportFilterCriteria
            {
                From = query.From,
                To = query.To,
                EventId = query.EventId,
                BookingStatus = query.BookingStatus,
                TicketTypeId = query.TicketTypeId
            }, allowedEventIds);

            uniqueCustomers = await bQuery
                .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Cancelled)
                .Select(b => b.CustomerId)
                .Distinct()
                .CountAsync(cancellationToken);

            activeEvents = await bQuery
                .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Cancelled)
                .Select(b => b.EventId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

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
