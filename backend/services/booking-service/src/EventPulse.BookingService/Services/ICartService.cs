using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

public interface ICartService
{
    /// <summary>
    /// Adds or updates a ticket type in the customer's active single-event cart.
    /// By default, Quantity represents the desired final count.
    /// If the customer already has an active cart for another event and ClearExisting is false,
    /// returns an EventConflict result.
    /// </summary>
    Task<CartOperationResult> AddOrUpdateItemAsync(
        Guid customerId,
        AddToCartRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the exact quantity of a ticket type in the active cart.
    /// If quantity <= 0, the item is removed.
    /// </summary>
    Task<(CartSummaryDto? Cart, string? Error)> SetItemQuantityAsync(
        Guid customerId,
        Guid ticketTypeId,
        int quantity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a ticket type from the customer's active cart.
    /// </summary>
    Task<(CartSummaryDto? Cart, string? Error)> RemoveItemAsync(
        Guid customerId,
        Guid ticketTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the customer's current active cart with live availability and event context.
    /// Completed or historical carts are never returned.
    /// </summary>
    Task<CartSummaryDto> GetActiveCartAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the customer's current active cart.
    /// </summary>
    Task<bool> ClearActiveCartAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the customer's current active cart as Completed (e.g. after successful checkout).
    /// </summary>
    Task<bool> CompleteActiveCartAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}