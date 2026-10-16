using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

public class BookingHistoryService : IBookingHistoryService
{
    private readonly BookingDbContext _dbContext;
    private readonly ILogger<BookingHistoryService> _logger;
    private readonly IEventAvailabilityClient? _eventClient;

    public BookingHistoryService(
        BookingDbContext dbContext,
        ILogger<BookingHistoryService> logger,
        IEventAvailabilityClient? eventClient = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _eventClient = eventClient;
    }

    public async Task<PagedResult<CustomerBookingHistoryDto>> GetCustomerBookingHistoryAsync(
        Guid customerId,
        BookingHistoryQueryParameters queryParams,
        CancellationToken ct = default)
    {
        // 1. Base query strictly scoped to caller's CustomerId
        var query = _dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.CustomerId == customerId);

        // 2. Status filtering if provided
        if (!string.IsNullOrWhiteSpace(queryParams.Status))
        {
            if (Enum.TryParse<BookingStatus>(queryParams.Status, ignoreCase: true, out var statusEnum))
            {
                query = query.Where(b => b.Status == statusEnum);
            }
            else
            {
                _logger.LogWarning("Invalid booking status filter supplied: {Status}", queryParams.Status);
            }
        }

        // 3. Count total items matching criteria
        var totalCount = await query.CountAsync(ct);

        // 4. Deterministic ordering and paging
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .Include(b => b.Items)
            .Select(b => new CustomerBookingHistoryDto(
                b.Id,
                b.BookingReference,
                b.EventId,
                b.Status.ToString(),
                b.TotalAmount,
                b.CreatedAt,
                b.ConfirmedAt,
                b.ExpiresAt,
                b.Items.Sum(i => i.Quantity),
                b.Items.Select(i => new BookingHistoryItemDto(
                    i.TicketTypeId,
                    i.TicketName,
                    i.Quantity,
                    i.UnitPrice,
                    i.Subtotal
                )).ToList(),
                null,
                null
            ))
            .ToListAsync(ct);

        // 5. Enrich with Event Name and Date via IEventAvailabilityClient if available
        if (_eventClient != null && items.Count > 0)
        {
            var distinctEventIds = items.Select(i => i.EventId).Distinct().ToList();
            var eventSummaries = new Dictionary<Guid, EventSummaryInfo?>();

            var fetchTasks = distinctEventIds.Select(async eventId =>
            {
                try
                {
                    var summary = await _eventClient.GetEventSummaryAsync(eventId, ct);
                    return (eventId, summary);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve event summary for EventId: {EventId}", eventId);
                    return (eventId, null as EventSummaryInfo);
                }
            });

            var results = await Task.WhenAll(fetchTasks);
            foreach (var (eventId, summary) in results)
            {
                eventSummaries[eventId] = summary;
            }

            items = items.Select(item =>
            {
                if (eventSummaries.TryGetValue(item.EventId, out var summary) && summary != null)
                {
                    return item with
                    {
                        EventName = summary.Title,
                        EventDate = summary.EventDate
                    };
                }
                return item;
            }).ToList();
        }

        return new PagedResult<CustomerBookingHistoryDto>
        {
            Items = items,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CustomerBookingDetailResult> GetCustomerBookingDetailAsync(
        Guid bookingId,
        Guid customerId,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        var booking = await _dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Items)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null)
        {
            return CustomerBookingDetailResult.NotFound();
        }

        if (booking.CustomerId != customerId && !isAdmin)
        {
            return CustomerBookingDetailResult.Forbidden();
        }

        string? eventName = null;
        DateTime? eventDate = null;
        string? eventVenue = null;

        if (_eventClient != null)
        {
            try
            {
                var eventSummary = await _eventClient.GetEventSummaryAsync(booking.EventId, ct);
                if (eventSummary != null)
                {
                    eventName = eventSummary.Title;
                    eventDate = eventSummary.EventDate;
                    eventVenue = eventSummary.Venue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch event summary for booking detail: {BookingId}, EventId: {EventId}", booking.Id, booking.EventId);
            }
        }

        var ticketDtos = booking.Tickets
            .OrderBy(t => t.TicketSequence)
            .Select(t => new CustomerTicketDto
            {
                TicketId = t.Id,
                TicketCode = t.TicketCode,
                BookingId = booking.Id,
                BookingReference = booking.BookingReference,
                EventId = booking.EventId,
                EventName = eventName,
                EventDate = eventDate,
                EventVenue = eventVenue,
                TicketTypeId = t.TicketTypeId,
                TicketName = t.TicketName,
                TicketSequence = t.TicketSequence,
                Status = t.Status.ToString(),
                ValidationToken = t.ValidationToken,
                QrPayload = $"eventpulse-ticket:{t.ValidationToken}",
                CreatedAt = t.CreatedAt
            })
            .ToList();

        var itemDtos = booking.Items
            .Select(i => new BookingHistoryItemDto(
                i.TicketTypeId,
                i.TicketName,
                i.Quantity,
                i.UnitPrice,
                i.Subtotal
            ))
            .ToList();

        var detailDto = new CustomerBookingDetailDto
        {
            Id = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            EventId = booking.EventId,
            EventName = eventName,
            EventDate = eventDate,
            EventVenue = eventVenue,
            Status = booking.Status.ToString(),
            TotalAmount = booking.TotalAmount,
            CreatedAt = booking.CreatedAt,
            ConfirmedAt = booking.ConfirmedAt,
            ExpiresAt = booking.ExpiresAt,
            TotalTickets = booking.Items.Sum(i => i.Quantity),
            Items = itemDtos,
            Tickets = ticketDtos
        };

        return CustomerBookingDetailResult.Success(detailDto);
    }
}
