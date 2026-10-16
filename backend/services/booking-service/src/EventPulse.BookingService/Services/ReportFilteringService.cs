using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

/// <summary>
/// Reusable backend implementation of reporting and analytics filtering logic (EP-394 / US-42).
/// Enforces multi-filter boolean AND logic, cross-service category and organizer resolution,
/// and line-item slicing for ticket type queries.
/// </summary>
public class ReportFilteringService : IReportFilteringService
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventAvailabilityClient _eventAvailabilityClient;
    private readonly ILogger<ReportFilteringService> _logger;

    public ReportFilteringService(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventAvailabilityClient,
        ILogger<ReportFilteringService> logger)
    {
        _dbContext = dbContext;
        _eventAvailabilityClient = eventAvailabilityClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>?> ResolveAllowedEventIdsAsync(
        Guid? organizerId,
        string? category,
        Guid? eventId = null,
        CancellationToken cancellationToken = default)
    {
        var hasOrganizer = organizerId.HasValue;
        var hasCategory = !string.IsNullOrWhiteSpace(category);
        var hasEvent = eventId.HasValue;

        if (!hasOrganizer && !hasCategory && !hasEvent)
        {
            return null; // Unrestricted event scope
        }

        try
        {
            // Case 1: Specific eventId requested
            if (hasEvent)
            {
                var targetEventId = eventId!.Value;

                // If neither organizer nor category is specified, targetEventId is the sole allowed event
                if (!hasOrganizer && !hasCategory)
                {
                    return new[] { targetEventId };
                }

                // Verify event against organizer if specified
                if (hasOrganizer)
                {
                    var orgEvents = await _eventAvailabilityClient.GetOrganizerEventsAsync(organizerId!.Value, cancellationToken);
                    if (!orgEvents.Any(e => e.Id == targetEventId))
                    {
                        _logger.LogInformation("Event {EventId} does not belong to Organizer {OrganizerId}", targetEventId, organizerId!.Value);
                        return Array.Empty<Guid>();
                    }
                }

                // Verify event against category if specified
                if (hasCategory)
                {
                    var normalizedCat = ReportCategories.Normalize(category) ?? category!.Trim();
                    var catEvents = await _eventAvailabilityClient.GetEventsByCategoryAsync(normalizedCat, cancellationToken);
                    if (!catEvents.Any(e => e.Id == targetEventId))
                    {
                        _logger.LogInformation("Event {EventId} does not match Category {Category}", targetEventId, normalizedCat);
                        return Array.Empty<Guid>();
                    }
                }

                return new[] { targetEventId };
            }

            // Case 2: No specific eventId; evaluate Organizer and/or Category
            List<Guid>? organizerEventIds = null;
            if (hasOrganizer)
            {
                var orgEvents = await _eventAvailabilityClient.GetOrganizerEventsAsync(organizerId!.Value, cancellationToken);
                organizerEventIds = orgEvents.Select(e => e.Id).ToList();
                if (organizerEventIds.Count == 0)
                {
                    return Array.Empty<Guid>();
                }
            }

            List<Guid>? categoryEventIds = null;
            if (hasCategory)
            {
                var normalizedCat = ReportCategories.Normalize(category) ?? category!.Trim();
                var catEvents = await _eventAvailabilityClient.GetEventsByCategoryAsync(normalizedCat, cancellationToken);
                categoryEventIds = catEvents.Select(e => e.Id).ToList();
                if (categoryEventIds.Count == 0)
                {
                    return Array.Empty<Guid>();
                }
            }

            if (organizerEventIds != null && categoryEventIds != null)
            {
                return organizerEventIds.Intersect(categoryEventIds).ToList();
            }

            return organizerEventIds ?? categoryEventIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving event filter boundary for Organizer: {OrganizerId}, Category: {Category}, EventId: {EventId}",
                organizerId, category, eventId);
            return Array.Empty<Guid>();
        }
    }

    public IQueryable<Booking> ApplyBookingFilters(
        IQueryable<Booking> query,
        ReportFilterCriteria criteria,
        IReadOnlyList<Guid>? allowedEventIds)
    {
        if (allowedEventIds != null)
        {
            query = query.Where(b => allowedEventIds.Contains(b.EventId));
        }

        if (criteria.EventId.HasValue)
        {
            query = query.Where(b => b.EventId == criteria.EventId.Value);
        }

        if (criteria.From.HasValue)
        {
            query = query.Where(b => (b.ConfirmedAt ?? b.CreatedAt) >= criteria.From.Value);
        }

        if (criteria.To.HasValue)
        {
            query = query.Where(b => (b.ConfirmedAt ?? b.CreatedAt) <= criteria.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.BookingStatus) &&
            !criteria.BookingStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<BookingStatus>(criteria.BookingStatus.Trim(), true, out var requestedStatus))
            {
                query = query.Where(b => b.Status == requestedStatus);
            }
        }

        if (criteria.TicketTypeId.HasValue)
        {
            query = query.Where(b => b.Items.Any(i => i.TicketTypeId == criteria.TicketTypeId.Value));
        }

        return query;
    }

    public IQueryable<SalesReport> ApplySalesReportFilters(
        IQueryable<SalesReport> query,
        ReportFilterCriteria criteria,
        IReadOnlyList<Guid>? allowedEventIds)
    {
        if (allowedEventIds != null)
        {
            query = query.Where(s => allowedEventIds.Contains(s.EventId));
        }

        if (criteria.EventId.HasValue)
        {
            query = query.Where(s => s.EventId == criteria.EventId.Value);
        }

        if (criteria.OrganizerId.HasValue)
        {
            query = query.Where(s => s.OrganizerId == criteria.OrganizerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Currency) &&
            !criteria.Currency.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedCurrency = criteria.Currency.Trim().ToUpperInvariant();
            query = query.Where(s => s.Currency.ToUpper() == normalizedCurrency);
        }

        if (criteria.From.HasValue)
        {
            query = query.Where(s => s.PaymentTimestampUtc >= criteria.From.Value);
        }

        if (criteria.To.HasValue)
        {
            query = query.Where(s => s.PaymentTimestampUtc <= criteria.To.Value);
        }

        if (criteria.TicketTypeId.HasValue)
        {
            query = query.Where(s => s.Items.Any(i => i.TicketTypeId == criteria.TicketTypeId.Value));
        }

        return query;
    }

    public IReadOnlyList<BookingItem> GetMatchingBookingItems(Booking booking, Guid? ticketTypeId = null)
    {
        if (booking?.Items == null)
            return Array.Empty<BookingItem>();

        if (!ticketTypeId.HasValue)
            return booking.Items.ToList();

        return booking.Items.Where(i => i.TicketTypeId == ticketTypeId.Value).ToList();
    }

    public int GetFilteredBookingTickets(Booking booking, Guid? ticketTypeId = null)
    {
        return GetMatchingBookingItems(booking, ticketTypeId).Sum(i => i.Quantity);
    }

    public IReadOnlyList<SalesReportItem> GetMatchingSalesItems(SalesReport report, Guid? ticketTypeId = null)
    {
        if (report?.Items == null)
            return Array.Empty<SalesReportItem>();

        if (!ticketTypeId.HasValue)
            return report.Items.ToList();

        return report.Items.Where(i => i.TicketTypeId == ticketTypeId.Value).ToList();
    }

    public decimal GetFilteredGrossRevenue(SalesReport report, Guid? ticketTypeId = null)
    {
        if (!ticketTypeId.HasValue)
            return report.SaleAmount;

        return GetMatchingSalesItems(report, ticketTypeId).Sum(i => i.Subtotal);
    }

    public decimal GetFilteredRefundedAmount(SalesReport report, Guid? ticketTypeId = null)
    {
        if (!ticketTypeId.HasValue)
            return report.RefundedAmount;

        return GetMatchingSalesItems(report, ticketTypeId).Sum(i => i.RefundedAmount);
    }

    public decimal GetFilteredNetRevenue(SalesReport report, Guid? ticketTypeId = null)
    {
        var gross = GetFilteredGrossRevenue(report, ticketTypeId);
        var refunded = GetFilteredRefundedAmount(report, ticketTypeId);
        return Math.Max(0m, gross - refunded);
    }

    public int GetFilteredTicketsSold(SalesReport report, Guid? ticketTypeId = null)
    {
        if (!ticketTypeId.HasValue)
            return report.Items.Sum(i => i.Quantity);

        return GetMatchingSalesItems(report, ticketTypeId).Sum(i => i.Quantity);
    }

    public int GetFilteredNetTicketsSold(SalesReport report, Guid? ticketTypeId = null)
    {
        if (!ticketTypeId.HasValue)
            return Math.Max(0, report.Items.Sum(i => i.Quantity - i.RefundedQuantity));

        return Math.Max(0, GetMatchingSalesItems(report, ticketTypeId).Sum(i => i.Quantity - i.RefundedQuantity));
    }
}
