using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

public class OrganizerBookingAnalyticsService : IOrganizerBookingAnalyticsService
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventAvailabilityClient _eventAvailabilityClient;
    private readonly ILogger<OrganizerBookingAnalyticsService> _logger;

    public OrganizerBookingAnalyticsService(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventAvailabilityClient,
        ILogger<OrganizerBookingAnalyticsService> logger)
    {
        _dbContext = dbContext;
        _eventAvailabilityClient = eventAvailabilityClient;
        _logger = logger;
    }

    public async Task<OrganizerBookingSummaryDto> GetOrganizerBookingSummaryAsync(
        Guid organizerId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EP-389: Computing overall booking summary for Organizer {OrganizerId} (Period: {From} to {To})",
            organizerId, from, to);

        var period = new ReportingPeriodDto { From = from, To = to };

        // 1. Resolve organizer's owned events
        IReadOnlyList<EventSummaryInfo> ownedEvents;
        try
        {
            ownedEvents = await _eventAvailabilityClient.GetOrganizerEventsAsync(organizerId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve owned events for Organizer {OrganizerId}", organizerId);
            ownedEvents = Array.Empty<EventSummaryInfo>();
        }

        if (ownedEvents.Count == 0)
        {
            return new OrganizerBookingSummaryDto { ReportingPeriod = period };
        }

        var ownedEventIds = ownedEvents.Select(e => e.Id).ToList();

        // 2. Query authoritative bookings for owned events
        var query = _dbContext.Bookings
            .Include(b => b.Items)
            .AsNoTracking()
            .Where(b => ownedEventIds.Contains(b.EventId))
            .Where(b => b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.Cancelled && (b.ConfirmedAt != null || b.CancelledAt != null)));

        if (from.HasValue)
            query = query.Where(b => (b.ConfirmedAt ?? b.CreatedAt) >= from.Value);

        if (to.HasValue)
            query = query.Where(b => (b.ConfirmedAt ?? b.CreatedAt) <= to.Value);

        var bookings = await query.ToListAsync(cancellationToken);

        if (bookings.Count == 0)
        {
            return new OrganizerBookingSummaryDto { ReportingPeriod = period };
        }

        // 3. Compute canonical metrics
        var confirmedBookings = bookings.Count(b => b.Status == BookingStatus.Confirmed);
        var cancelledBookings = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        var totalBookings = confirmedBookings + cancelledBookings;

        var activeTickets = bookings
            .Where(b => b.Status == BookingStatus.Confirmed)
            .SelectMany(b => b.Items)
            .Sum(i => i.Quantity);

        var cancelledTickets = bookings
            .Where(b => b.Status == BookingStatus.Cancelled)
            .SelectMany(b => b.Items)
            .Sum(i => i.Quantity);

        var totalTickets = activeTickets + cancelledTickets;

        var cancellationRate = totalBookings > 0
            ? Math.Round(((decimal)cancelledBookings / totalBookings) * 100m, 2)
            : 0m;

        var ticketCancellationRate = totalTickets > 0
            ? Math.Round(((decimal)cancelledTickets / totalTickets) * 100m, 2)
            : 0m;

        var avgTicketsPerBooking = confirmedBookings > 0
            ? Math.Round((decimal)activeTickets / confirmedBookings, 2)
            : (totalBookings > 0 ? Math.Round((decimal)totalTickets / totalBookings, 2) : 0m);

        return new OrganizerBookingSummaryDto
        {
            TotalBookings = totalBookings,
            ConfirmedBookings = confirmedBookings,
            CancelledBookings = cancelledBookings,
            TotalTicketsBooked = totalTickets,
            ActiveTicketsBooked = activeTickets,
            CancelledTickets = cancelledTickets,
            CancellationRate = cancellationRate,
            TicketCancellationRate = ticketCancellationRate,
            AverageTicketsPerBooking = avgTicketsPerBooking,
            ReportingPeriod = period
        };
    }

    public async Task<EventBookingAnalyticsDto?> GetEventBookingAnalyticsAsync(
        Guid organizerId,
        Guid eventId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EP-389: Computing event booking analytics for Event {EventId}, requested by Organizer {OrganizerId}",
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
            // Fallback: Check if event is present in organizer's owned events list
            var ownedEvents = await _eventAvailabilityClient.GetOrganizerEventsAsync(organizerId, cancellationToken);
            var match = ownedEvents.FirstOrDefault(e => e.Id == eventId);
            if (match == null)
            {
                _logger.LogInformation("Event {EventId} not found or not owned by Organizer {OrganizerId}", eventId, organizerId);
                return null;
            }
            eventSummary = match;
        }

        // 2. Fetch live ticket tier availability from Event Service
        IReadOnlyList<TicketTypeAvailability> liveTicketTypes;
        try
        {
            liveTicketTypes = await _eventAvailabilityClient.GetEventTicketTypesAsync(eventId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch live ticket types for {EventId}", eventId);
            liveTicketTypes = Array.Empty<TicketTypeAvailability>();
        }

        int totalCapacity = liveTicketTypes?.Sum(t => t.TotalQuantity) ?? 0;
        int remainingCapacity = liveTicketTypes?.Sum(t => t.RemainingQuantity) ?? 0;

        // 3. Query bookings for this event
        var query = _dbContext.Bookings
            .Include(b => b.Items)
            .AsNoTracking()
            .Where(b => b.EventId == eventId)
            .Where(b => b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.Cancelled && (b.ConfirmedAt != null || b.CancelledAt != null)));

        if (from.HasValue)
            query = query.Where(b => (b.ConfirmedAt ?? b.CreatedAt) >= from.Value);

        if (to.HasValue)
            query = query.Where(b => (b.ConfirmedAt ?? b.CreatedAt) <= to.Value);

        var eventBookings = await query.ToListAsync(cancellationToken);

        var period = new ReportingPeriodDto { From = from, To = to };
        var eventName = eventSummary?.Title ?? "Event";

        var confirmedBookings = eventBookings.Count(b => b.Status == BookingStatus.Confirmed);
        var cancelledBookings = eventBookings.Count(b => b.Status == BookingStatus.Cancelled);
        var totalBookings = confirmedBookings + cancelledBookings;

        var activeTickets = eventBookings
            .Where(b => b.Status == BookingStatus.Confirmed)
            .SelectMany(b => b.Items)
            .Sum(i => i.Quantity);

        var cancelledTickets = eventBookings
            .Where(b => b.Status == BookingStatus.Cancelled)
            .SelectMany(b => b.Items)
            .Sum(i => i.Quantity);

        var totalTickets = activeTickets + cancelledTickets;

        var cancellationRate = totalBookings > 0
            ? Math.Round(((decimal)cancelledBookings / totalBookings) * 100m, 2)
            : 0m;

        var ticketCancellationRate = totalTickets > 0
            ? Math.Round(((decimal)cancelledTickets / totalTickets) * 100m, 2)
            : 0m;

        var capacityUtilization = totalCapacity > 0
            ? Math.Round(((decimal)activeTickets / totalCapacity) * 100m, 2)
            : 0m;

        // 4. Ticket tiers breakdown
        var allItems = eventBookings.SelectMany(b => b.Items).ToList();
        var allTierIds = allItems.Select(i => i.TicketTypeId)
            .Union(liveTicketTypes?.Select(t => t.Id) ?? Enumerable.Empty<Guid>())
            .Distinct()
            .ToList();

        var ticketTypesBreakdown = new List<TicketTypeBookingSummaryDto>();
        foreach (var tierId in allTierIds)
        {
            var liveTier = liveTicketTypes?.FirstOrDefault(t => t.Id == tierId);
            var tierItems = allItems.Where(i => i.TicketTypeId == tierId).ToList();

            var tierName = liveTier?.Name
                ?? tierItems.FirstOrDefault()?.TicketName
                ?? "General Admission";

            var tierBookingsCount = eventBookings
                .Count(b => b.Status == BookingStatus.Confirmed && b.Items.Any(i => i.TicketTypeId == tierId));

            var tierActiveTickets = eventBookings
                .Where(b => b.Status == BookingStatus.Confirmed)
                .SelectMany(b => b.Items.Where(i => i.TicketTypeId == tierId))
                .Sum(i => i.Quantity);

            var tierCancelledTickets = eventBookings
                .Where(b => b.Status == BookingStatus.Cancelled)
                .SelectMany(b => b.Items.Where(i => i.TicketTypeId == tierId))
                .Sum(i => i.Quantity);

            ticketTypesBreakdown.Add(new TicketTypeBookingSummaryDto
            {
                TicketTypeId = tierId,
                TicketTypeName = tierName,
                BookingsCount = tierBookingsCount,
                TicketsBooked = tierActiveTickets + tierCancelledTickets,
                ActiveTickets = tierActiveTickets,
                CancelledTickets = tierCancelledTickets,
                Capacity = liveTier?.TotalQuantity ?? 0,
                RemainingCapacity = liveTier?.RemainingQuantity ?? 0
            });
        }

        return new EventBookingAnalyticsDto
        {
            EventId = eventId,
            EventName = eventName,
            TotalBookings = totalBookings,
            ConfirmedBookings = confirmedBookings,
            CancelledBookings = cancelledBookings,
            TotalTicketsBooked = totalTickets,
            ActiveTicketsBooked = activeTickets,
            CancelledTickets = cancelledTickets,
            CancellationRate = cancellationRate,
            TicketCancellationRate = ticketCancellationRate,
            TotalCapacity = totalCapacity,
            RemainingCapacity = remainingCapacity,
            CapacityUtilizationRate = capacityUtilization,
            ReportingPeriod = period,
            TicketTypes = ticketTypesBreakdown.OrderByDescending(t => t.TicketsBooked).ToList()
        };
    }

    public async Task<PagedResult<EventBookingSummaryItemDto>> GetOrganizerEventsBookingListAsync(
        Guid organizerId,
        string? search = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EP-389: Retrieving events booking summary list for Organizer {OrganizerId} (Page {Page}, PageSize {PageSize})",
            organizerId, page, pageSize);

        // 1. Resolve organizer's owned events
        var ownedEvents = await _eventAvailabilityClient.GetOrganizerEventsAsync(organizerId, cancellationToken);
        if (ownedEvents.Count == 0)
        {
            return new PagedResult<EventBookingSummaryItemDto>
            {
                Items = new List<EventBookingSummaryItemDto>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };
        }

        var filteredEvents = ownedEvents.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredEvents = filteredEvents.Where(e =>
                e.Title.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                e.Venue.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var eventList = filteredEvents.ToList();
        var totalCount = eventList.Count;

        var pagedEvents = eventList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        if (pagedEvents.Count == 0)
        {
            return new PagedResult<EventBookingSummaryItemDto>
            {
                Items = new List<EventBookingSummaryItemDto>(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        var pagedEventIds = pagedEvents.Select(e => e.Id).ToList();

        // 2. Query bookings for this page of events
        var query = _dbContext.Bookings
            .Include(b => b.Items)
            .AsNoTracking()
            .Where(b => pagedEventIds.Contains(b.EventId))
            .Where(b => b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.Cancelled && (b.ConfirmedAt != null || b.CancelledAt != null)));

        if (from.HasValue)
            query = query.Where(b => (b.ConfirmedAt ?? b.CreatedAt) >= from.Value);

        if (to.HasValue)
            query = query.Where(b => (b.ConfirmedAt ?? b.CreatedAt) <= to.Value);

        var bookings = await query.ToListAsync(cancellationToken);
        var bookingsByEvent = bookings.GroupBy(b => b.EventId).ToDictionary(g => g.Key, g => g.ToList());

        var items = new List<EventBookingSummaryItemDto>();
        foreach (var ev in pagedEvents)
        {
            bookingsByEvent.TryGetValue(ev.Id, out var evBookings);
            evBookings ??= new List<Booking>();

            var confirmed = evBookings.Count(b => b.Status == BookingStatus.Confirmed);
            var cancelled = evBookings.Count(b => b.Status == BookingStatus.Cancelled);
            var total = confirmed + cancelled;

            var activeTickets = evBookings
                .Where(b => b.Status == BookingStatus.Confirmed)
                .SelectMany(b => b.Items)
                .Sum(i => i.Quantity);

            var cancelledTickets = evBookings
                .Where(b => b.Status == BookingStatus.Cancelled)
                .SelectMany(b => b.Items)
                .Sum(i => i.Quantity);

            var totalTickets = activeTickets + cancelledTickets;

            var cancelRate = total > 0
                ? Math.Round(((decimal)cancelled / total) * 100m, 2)
                : 0m;

            var lastBooking = evBookings
                .OrderByDescending(b => b.ConfirmedAt ?? b.CreatedAt)
                .FirstOrDefault();

            // Sourced capacity
            IReadOnlyList<TicketTypeAvailability> tiers = Array.Empty<TicketTypeAvailability>();
            try
            {
                tiers = await _eventAvailabilityClient.GetEventTicketTypesAsync(ev.Id, cancellationToken);
            }
            catch
            {
                // Non-critical fallback
            }

            var totalCap = tiers?.Sum(t => t.TotalQuantity) ?? 0;
            var remCap = tiers?.Sum(t => t.RemainingQuantity) ?? 0;

            items.Add(new EventBookingSummaryItemDto
            {
                EventId = ev.Id,
                EventName = ev.Title,
                TotalBookings = total,
                ConfirmedBookings = confirmed,
                CancelledBookings = cancelled,
                TotalTicketsBooked = totalTickets,
                ActiveTicketsBooked = activeTickets,
                CancellationRate = cancelRate,
                TotalCapacity = totalCap,
                RemainingCapacity = remCap,
                LastBookingAtUtc = lastBooking?.ConfirmedAt ?? lastBooking?.CreatedAt
            });
        }

        return new PagedResult<EventBookingSummaryItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
