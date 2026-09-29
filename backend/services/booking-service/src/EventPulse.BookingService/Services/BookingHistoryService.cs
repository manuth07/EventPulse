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

    public BookingHistoryService(
        BookingDbContext dbContext,
        ILogger<BookingHistoryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
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
                b.Items.Sum(i => i.Quantity),
                b.Items.Select(i => new BookingHistoryItemDto(
                    i.TicketTypeId,
                    i.TicketName,
                    i.Quantity,
                    i.UnitPrice,
                    i.Subtotal
                )).ToList()
            ))
            .ToListAsync(ct);

        return new PagedResult<CustomerBookingHistoryDto>
        {
            Items = items,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize,
            TotalCount = totalCount
        };
    }
}
