using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

public interface IBookingPaymentFailureService
{
    Task<BookingPaymentFailureResult> HandlePaymentFailureAsync(
        Guid bookingId,
        string? failureCode = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default);
}
