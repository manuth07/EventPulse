using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

public class OrganizerSalesAnalyticsService : IOrganizerSalesAnalyticsService
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventAvailabilityClient _eventAvailabilityClient;
    private readonly ILogger<OrganizerSalesAnalyticsService> _logger;

    public OrganizerSalesAnalyticsService(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventAvailabilityClient,
        ILogger<OrganizerSalesAnalyticsService> logger)
    {
        _dbContext = dbContext;
        _eventAvailabilityClient = eventAvailabilityClient;
        _logger = logger;
    }

    public async Task<OrganizerSalesSummaryDto> GetOrganizerSalesSummaryAsync(
        Guid organizerId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EP-384: Computing overall sales summary for Organizer {OrganizerId} (Period: {From} to {To})",
            organizerId, from, to);

        var query = _dbContext.SalesReports
            .Include(s => s.Items)
            .AsNoTracking()
            .Where(s => s.OrganizerId == organizerId);

        if (from.HasValue)
            query = query.Where(s => s.PaymentTimestampUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(s => s.PaymentTimestampUtc <= to.Value);

        var reports = await query.ToListAsync(cancellationToken);

        var period = new ReportingPeriodDto { From = from, To = to };

        if (reports.Count == 0)
        {
            return new OrganizerSalesSummaryDto
            {
                GrossRevenue = 0m,
                RefundedAmount = 0m,
                NetRevenue = 0m,
                TicketsSold = 0,
                NetTicketsSold = 0,
                SuccessfulPurchaseCount = 0,
                Currency = "USD",
                ReportingPeriod = period,
                Currencies = new List<CurrencySalesSummaryDto>()
            };
        }

        // Group by currency to avoid invalid mixing of multi-currency amounts
        var currencyGroups = reports
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Currency) ? "USD" : r.Currency.ToUpperInvariant())
            .Select(g =>
            {
                var gross = g.Sum(r => r.SaleAmount);
                var refunded = g.Sum(r => r.RefundedAmount);
                var ticketsSold = g.SelectMany(r => r.Items).Sum(i => i.Quantity);
                var netTicketsSold = g.SelectMany(r => r.Items).Sum(i => i.Quantity - i.RefundedQuantity);
                var purchaseCount = g.Count();

                return new CurrencySalesSummaryDto
                {
                    Currency = g.Key,
                    GrossRevenue = gross,
                    RefundedAmount = refunded,
                    NetRevenue = Math.Max(0m, gross - refunded),
                    TicketsSold = ticketsSold,
                    NetTicketsSold = Math.Max(0, netTicketsSold),
                    SuccessfulPurchaseCount = purchaseCount
                };
            })
            .OrderByDescending(c => c.GrossRevenue)
            .ToList();

        if (currencyGroups.Count == 1)
        {
            var primary = currencyGroups[0];
            return new OrganizerSalesSummaryDto
            {
                GrossRevenue = primary.GrossRevenue,
                RefundedAmount = primary.RefundedAmount,
                NetRevenue = primary.NetRevenue,
                TicketsSold = primary.TicketsSold,
                NetTicketsSold = primary.NetTicketsSold,
                SuccessfulPurchaseCount = primary.SuccessfulPurchaseCount,
                Currency = primary.Currency,
                ReportingPeriod = period,
                Currencies = currencyGroups
            };
        }

        // Multiple currencies: financial metrics are maintained separately in Currencies to avoid arbitrary conversion
        var totalTicketsSold = reports.SelectMany(r => r.Items).Sum(i => i.Quantity);
        var totalNetTicketsSold = reports.SelectMany(r => r.Items).Sum(i => Math.Max(0, i.Quantity - i.RefundedQuantity));
        var totalPurchaseCount = reports.Count;

        return new OrganizerSalesSummaryDto
        {
            GrossRevenue = 0m,
            RefundedAmount = 0m,
            NetRevenue = 0m,
            TicketsSold = totalTicketsSold,
            NetTicketsSold = totalNetTicketsSold,
            SuccessfulPurchaseCount = totalPurchaseCount,
            Currency = "MULTIPLE",
            ReportingPeriod = period,
            Currencies = currencyGroups
        };
    }

    public async Task<EventSalesAnalyticsDto?> GetEventSalesAnalyticsAsync(
        Guid organizerId,
        Guid eventId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EP-384: Computing event sales analytics for Event {EventId}, requested by Organizer {OrganizerId}",
            eventId, organizerId);

        // 1. Verify Event Ownership server-side
        EventSummaryInfo? eventSummary = null;
        try
        {
            eventSummary = await _eventAvailabilityClient.GetEventSummaryAsync(eventId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch event summary for {EventId} from EventService", eventId);
        }

        if (eventSummary != null)
        {
            if (eventSummary.OrganizerId != organizerId)
            {
                _logger.LogWarning(
                    "Security violation: Organizer {OrganizerId} tried to access Event {EventId} owned by {OwnerId}",
                    organizerId, eventId, eventSummary.OrganizerId);
                return null; // Non-disclosing 404
            }
        }
        else
        {
            // Fallback check against recorded sales reports
            var existingSale = await _dbContext.SalesReports
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.EventId == eventId, cancellationToken);

            if (existingSale == null)
            {
                _logger.LogInformation("Event {EventId} not found in EventService or SalesReports", eventId);
                return null;
            }

            if (existingSale.OrganizerId != organizerId)
            {
                _logger.LogWarning(
                    "Security violation: Organizer {OrganizerId} tried to access Event {EventId} owned by {OwnerId}",
                    organizerId, eventId, existingSale.OrganizerId);
                return null;
            }
        }

        // 2. Query matching sales reports for this event
        var query = _dbContext.SalesReports
            .Include(s => s.Items)
            .AsNoTracking()
            .Where(s => s.EventId == eventId && s.OrganizerId == organizerId);

        if (from.HasValue)
            query = query.Where(s => s.PaymentTimestampUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(s => s.PaymentTimestampUtc <= to.Value);

        var eventSales = await query.ToListAsync(cancellationToken);

        var period = new ReportingPeriodDto { From = from, To = to };
        var eventName = eventSummary?.Title ?? "Event";

        if (eventSales.Count == 0)
        {
            return new EventSalesAnalyticsDto
            {
                EventId = eventId,
                EventName = eventName,
                GrossRevenue = 0m,
                RefundedAmount = 0m,
                NetRevenue = 0m,
                TicketsSold = 0,
                NetTicketsSold = 0,
                SuccessfulPurchaseCount = 0,
                Currency = "USD",
                ReportingPeriod = period,
                TicketTypes = new List<TicketTypeSalesSummaryDto>()
            };
        }

        var grossRevenue = eventSales.Sum(s => s.SaleAmount);
        var refundedAmount = eventSales.Sum(s => s.RefundedAmount);
        var netRevenue = Math.Max(0m, grossRevenue - refundedAmount);
        var totalTicketsSold = eventSales.SelectMany(s => s.Items).Sum(i => i.Quantity);
        var netTicketsSold = eventSales.SelectMany(s => s.Items).Sum(i => i.Quantity - i.RefundedQuantity);
        var purchaseCount = eventSales.Count;
        var currency = eventSales.FirstOrDefault()?.Currency ?? "USD";

        // 3. Compute revenue per ticket type breakdown
        var ticketTypeSummaries = eventSales.SelectMany(s => s.Items)
            .GroupBy(i => i.TicketTypeId)
            .Select(g =>
            {
                var qtySold = g.Sum(i => i.Quantity);
                var refQty = g.Sum(i => i.RefundedQuantity);
                var gross = g.Sum(i => i.Subtotal);
                var refAmount = g.Sum(i => i.RefundedAmount);
                var net = Math.Max(0m, gross - refAmount);
                var first = g.First();

                return new TicketTypeSalesSummaryDto
                {
                    TicketTypeId = g.Key,
                    TicketTypeName = first.TicketTypeName,
                    QuantitySold = qtySold,
                    RefundedQuantity = refQty,
                    NetQuantitySold = Math.Max(0, qtySold - refQty),
                    UnitPrice = qtySold > 0 ? (gross / qtySold) : first.UnitPrice,
                    GrossRevenue = gross,
                    RefundedAmount = refAmount,
                    NetRevenue = net
                };
            })
            .OrderByDescending(t => t.GrossRevenue)
            .ToList();

        return new EventSalesAnalyticsDto
        {
            EventId = eventId,
            EventName = eventName,
            GrossRevenue = grossRevenue,
            RefundedAmount = refundedAmount,
            NetRevenue = netRevenue,
            TicketsSold = totalTicketsSold,
            NetTicketsSold = Math.Max(0, netTicketsSold),
            SuccessfulPurchaseCount = purchaseCount,
            Currency = currency,
            ReportingPeriod = period,
            TicketTypes = ticketTypeSummaries
        };
    }

    public async Task<PagedResult<EventSalesSummaryItemDto>> GetOrganizerEventsSalesListAsync(
        Guid organizerId,
        string? search = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EP-384: Retrieving events sales list for Organizer {OrganizerId} (Page: {Page}, PageSize: {PageSize})",
            organizerId, page, pageSize);

        // 1. Resolve all events owned by organizer from EventService
        var eventDict = new Dictionary<Guid, string>();
        try
        {
            var organizerEvents = await _eventAvailabilityClient.GetOrganizerEventsAsync(organizerId, cancellationToken);
            foreach (var evt in organizerEvents)
            {
                eventDict[evt.Id] = evt.Title;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load organizer events from EventService. Using local sales records.");
        }

        // 2. Query SalesReports for this organizer
        var query = _dbContext.SalesReports
            .Include(s => s.Items)
            .AsNoTracking()
            .Where(s => s.OrganizerId == organizerId);

        if (from.HasValue)
            query = query.Where(s => s.PaymentTimestampUtc >= from.Value);

        if (to.HasValue)
            query = query.Where(s => s.PaymentTimestampUtc <= to.Value);

        var sales = await query.ToListAsync(cancellationToken);

        // Include any event IDs found in sales reports
        var salesByEvent = sales.GroupBy(s => s.EventId).ToDictionary(g => g.Key, g => g.ToList());

        var allEventIds = new HashSet<Guid>(eventDict.Keys);
        foreach (var evtId in salesByEvent.Keys)
        {
            allEventIds.Add(evtId);
            if (!eventDict.ContainsKey(evtId))
            {
                eventDict[evtId] = "Event";
            }
        }

        var items = new List<EventSalesSummaryItemDto>();

        foreach (var evtId in allEventIds)
        {
            var title = eventDict.TryGetValue(evtId, out var name) ? name : "Event";

            if (salesByEvent.TryGetValue(evtId, out var eventSales) && eventSales.Count > 0)
            {
                var gross = eventSales.Sum(s => s.SaleAmount);
                var refunded = eventSales.Sum(s => s.RefundedAmount);
                var net = Math.Max(0m, gross - refunded);
                var ticketsSold = eventSales.SelectMany(s => s.Items).Sum(i => i.Quantity);
                var netTicketsSold = eventSales.SelectMany(s => s.Items).Sum(i => i.Quantity - i.RefundedQuantity);
                var purchaseCount = eventSales.Count;
                var currency = eventSales.FirstOrDefault()?.Currency ?? "USD";
                var lastSaleAt = eventSales.Max(s => (DateTimeOffset?)s.PaymentTimestampUtc);

                items.Add(new EventSalesSummaryItemDto
                {
                    EventId = evtId,
                    EventName = title,
                    GrossRevenue = gross,
                    RefundedAmount = refunded,
                    NetRevenue = net,
                    TicketsSold = ticketsSold,
                    NetTicketsSold = Math.Max(0, netTicketsSold),
                    SuccessfulPurchaseCount = purchaseCount,
                    Currency = currency,
                    LastSaleAtUtc = lastSaleAt
                });
            }
            else
            {
                // Event with no sales
                items.Add(new EventSalesSummaryItemDto
                {
                    EventId = evtId,
                    EventName = title,
                    GrossRevenue = 0m,
                    RefundedAmount = 0m,
                    NetRevenue = 0m,
                    TicketsSold = 0,
                    NetTicketsSold = 0,
                    SuccessfulPurchaseCount = 0,
                    Currency = "USD",
                    LastSaleAtUtc = null
                });
            }
        }

        // Apply title search filter if specified
        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(i => i.EventName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Order by last sale descending, then gross revenue descending
        var ordered = items
            .OrderByDescending(i => i.LastSaleAtUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(i => i.GrossRevenue)
            .ToList();

        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<EventSalesSummaryItemDto>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
