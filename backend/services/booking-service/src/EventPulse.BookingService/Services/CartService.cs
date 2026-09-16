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

    public async Task<CartOperationResult> AddOrUpdateItemAsync(
        Guid customerId,
        AddToCartRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity < 0)
            return CartOperationResult.Fail("Quantity cannot be negative.");

        // 1. Locate current active cart for customer
        var activeCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active, cancellationToken);

        // 2. Single-Event Rule enforcement
        if (activeCart != null && activeCart.EventId != request.EventId)
        {
            if (activeCart.Items.Count > 0)
            {
                if (!request.ClearExisting)
                {
                    // Fetch existing event title for friendly modal prompt
                    var existingEvent = await _availabilityClient.GetEventSummaryAsync(activeCart.EventId, cancellationToken);
                    var existingTitle = !string.IsNullOrWhiteSpace(existingEvent?.Title) ? existingEvent.Title : "another event";

                    return CartOperationResult.EventConflict(new EventConflictDto
                    {
                        CurrentEventId = activeCart.EventId,
                        CurrentEventTitle = existingTitle,
                        AttemptedEventId = request.EventId,
                        Message = $"Your cart currently contains tickets for {existingTitle}. Starting a new ticket selection will clear your current cart."
                    });
                }

                // Explicit customer confirmation: Clear/abandon previous cart
                activeCart.Status = CartStatus.Abandoned;
                activeCart.UpdatedAt = DateTime.UtcNow;
                activeCart = null; // will create a new active cart below
            }
            else
            {
                // Previous cart has 0 items, simply switch event
                activeCart.EventId = request.EventId;
                activeCart.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 3. Create active cart if needed
        if (activeCart == null)
        {
            activeCart = new Cart
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                EventId = request.EventId,
                Status = CartStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.Carts.Add(activeCart);
        }

        // 4. Validate live availability and purchasability with EventService
        var availability = await _availabilityClient.GetTicketTypeAsync(request.EventId, request.TicketTypeId, cancellationToken);
        if (availability == null)
            return CartOperationResult.Fail("This ticket type is no longer available.");

        if (availability.IsSoldOut)
            return CartOperationResult.Fail("This ticket type is sold out.");

        var eventSummary = await _availabilityClient.GetEventSummaryAsync(request.EventId, cancellationToken);
        if (eventSummary != null && eventSummary.Status != null && eventSummary.Status != "Published")
        {
            return CartOperationResult.Fail("Tickets for this event are not currently available for purchase.");
        }

        // 5. Calculate target quantity (preferred: desired final count, with delta support if specified)
        var existingItem = activeCart.Items.FirstOrDefault(i => i.TicketTypeId == request.TicketTypeId);
        int targetQuantity = request.IsDelta
            ? (existingItem?.Quantity ?? 0) + request.Quantity
            : request.Quantity;

        if (targetQuantity <= 0)
        {
            if (existingItem != null)
            {
                _context.CartItems.Remove(existingItem);
                activeCart.Items.Remove(existingItem);
            }
        }
        else
        {
            if (targetQuantity > availability.AvailableQuantity)
            {
                return CartOperationResult.Fail(
                    $"Only {availability.AvailableQuantity} ticket(s) available. You requested {targetQuantity}.");
            }

            if (existingItem != null)
            {
                existingItem.Quantity = targetQuantity;
                existingItem.UnitPrice = availability.Price; // authoritative server-side price update
                existingItem.TicketTypeName = availability.Name;
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                existingItem = new CartItem
                {
                    Id = Guid.NewGuid(),
                    CartId = activeCart.Id,
                    CustomerId = customerId,
                    EventId = request.EventId,
                    TicketTypeId = request.TicketTypeId,
                    TicketTypeName = availability.Name,
                    UnitPrice = availability.Price,
                    Quantity = targetQuantity,
                    AddedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                activeCart.Items.Add(existingItem);
                _context.CartItems.Add(existingItem);
            }
        }

        activeCart.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist cart changes for Customer {CustomerId}", customerId);
            return CartOperationResult.Fail("Failed to update cart. Please try again.");
        }

        var dto = await MapCartDtoAsync(activeCart, eventSummary, cancellationToken);
        return CartOperationResult.Ok(dto);
    }

    public async Task<(CartSummaryDto? Cart, string? Error)> SetItemQuantityAsync(
        Guid customerId,
        Guid ticketTypeId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var activeCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active, cancellationToken);

        if (activeCart == null)
            return (null, "No active cart found.");

        var item = activeCart.Items.FirstOrDefault(i => i.TicketTypeId == ticketTypeId);
        if (item == null)
            return (null, "Ticket type not found in cart.");

        if (quantity <= 0)
        {
            _context.CartItems.Remove(item);
            activeCart.Items.Remove(item);
        }
        else
        {
            var availability = await _availabilityClient.GetTicketTypeAsync(activeCart.EventId, ticketTypeId, cancellationToken);
            if (availability == null || availability.IsSoldOut)
                return (null, "This ticket type is no longer available.");

            if (quantity > availability.AvailableQuantity)
                return (null, $"Only {availability.AvailableQuantity} ticket(s) currently available.");

            item.Quantity = quantity;
            item.UnitPrice = availability.Price;
            item.TicketTypeName = availability.Name;
            item.UpdatedAt = DateTime.UtcNow;
        }

        activeCart.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update item quantity for Customer {CustomerId}", customerId);
            return (null, "Failed to update quantity. Please try again.");
        }

        var dto = await MapCartDtoAsync(activeCart, null, cancellationToken);
        return (dto, null);
    }

    public async Task<(CartSummaryDto? Cart, string? Error)> RemoveItemAsync(
        Guid customerId,
        Guid ticketTypeId,
        CancellationToken cancellationToken = default)
    {
        return await SetItemQuantityAsync(customerId, ticketTypeId, 0, cancellationToken);
    }

    public async Task<CartSummaryDto> GetActiveCartAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var activeCart = await _context.Carts
            .Include(c => c.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active, cancellationToken);

        if (activeCart == null || activeCart.Items.Count == 0)
        {
            return new CartSummaryDto { Items = new List<CartItemDto>() };
        }

        return await MapCartDtoAsync(activeCart, null, cancellationToken);
    }

    public async Task<bool> ClearActiveCartAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var activeCart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active, cancellationToken);

        if (activeCart == null)
            return true;

        _context.CartItems.RemoveRange(activeCart.Items);
        activeCart.Status = CartStatus.Abandoned;
        activeCart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteActiveCartAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var activeCart = await _context.Carts
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active, cancellationToken);

        if (activeCart == null)
            return false;

        activeCart.Status = CartStatus.Completed;
        activeCart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<CartSummaryDto> MapCartDtoAsync(
        Cart cart,
        EventSummaryInfo? eventSummary = null,
        CancellationToken cancellationToken = default)
    {
        eventSummary ??= await _availabilityClient.GetEventSummaryAsync(cart.EventId, cancellationToken);

        return new CartSummaryDto
        {
            CartId = cart.Id,
            EventId = cart.EventId,
            EventTitle = eventSummary?.Title ?? "Event Tickets",
            EventVenue = eventSummary?.Venue,
            EventDate = eventSummary?.EventDate,
            EventImageUrl = eventSummary?.ImageUrl,
            Items = cart.Items
                .OrderBy(i => i.AddedAt)
                .Select(i => new CartItemDto
                {
                    Id = i.Id,
                    CartId = i.CartId,
                    EventId = i.EventId,
                    TicketTypeId = i.TicketTypeId,
                    TicketTypeName = i.TicketTypeName,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                })
                .ToList()
        };
    }
}