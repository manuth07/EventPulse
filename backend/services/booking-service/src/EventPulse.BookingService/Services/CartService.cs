using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

public class CartService : ICartService
{
    private readonly BookingDbContext _context;
    private readonly IEventAvailabilityClient _availabilityClient;
    private readonly ILogger<CartService>? _logger;

    public CartService(
        BookingDbContext context,
        IEventAvailabilityClient availabilityClient,
        ILogger<CartService>? logger = null)
    {
        _context = context;
        _availabilityClient = availabilityClient;
        _logger = logger;
    }

    public async Task<(CartItemDto? Result, string? Error)> AddToCartAsync(
        Guid customerId,
        AddToCartRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity < 1)
            return (null, "Quantity must be at least 1.");

        var availability = await _availabilityClient.GetTicketTypeAsync(request.EventId, request.TicketTypeId, cancellationToken);

        if (availability == null)
            return (null, "This ticket type is no longer available.");

        if (availability.IsSoldOut)
            return (null, "This ticket type is sold out.");

        var existing = await _context.CartItems.FirstOrDefaultAsync(
            c => c.CustomerId == customerId && c.TicketTypeId == request.TicketTypeId, cancellationToken);

        var requestedTotal = (existing?.Quantity ?? 0) + request.Quantity;

        if (requestedTotal > availability.AvailableQuantity)
            return (null,
                $"Only {availability.AvailableQuantity} ticket(s) available. You already have {existing?.Quantity ?? 0} in your cart.");

        if (existing != null)
        {
            existing.Quantity = requestedTotal;
            existing.UnitPrice = availability.Price; // keep price in sync in case it changed
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new CartItem
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                EventId = request.EventId,
                TicketTypeId = request.TicketTypeId,
                TicketTypeName = availability.Name,
                UnitPrice = availability.Price,
                Quantity = request.Quantity,
                AddedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.CartItems.Add(existing);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "DB save failed while adding to cart for Customer {CustomerId}", customerId);
            return (null, "Failed to add to cart. Please try again.");
        }

        return (MapToDto(existing), null);
    }

    public async Task<CartSummaryDto> GetCartAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var items = await _context.CartItems
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .OrderBy(c => c.AddedAt)
            .ToListAsync(cancellationToken);

        return new CartSummaryDto { Items = items.Select(MapToDto).ToList() };
    }

    private static CartItemDto MapToDto(CartItem c) => new CartItemDto
    {
        Id = c.Id,
        EventId = c.EventId,
        TicketTypeId = c.TicketTypeId,
        TicketTypeName = c.TicketTypeName,
        UnitPrice = c.UnitPrice,
        Quantity = c.Quantity,
    };
}