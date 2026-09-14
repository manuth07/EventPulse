using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

public interface ICartService
{
    /// <summary>
    /// Adds a ticket type to the customer's cart, or increments quantity if
    /// already present. Validates requested quantity against live availability.
    /// </summary>
    Task<(CartItemDto? Result, string? Error)> AddToCartAsync(
        Guid customerId,
        AddToCartRequest request,
        CancellationToken cancellationToken = default);

    Task<CartSummaryDto> GetCartAsync(Guid customerId, CancellationToken cancellationToken = default);
}