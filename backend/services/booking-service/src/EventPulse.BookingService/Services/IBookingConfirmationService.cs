using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

public interface IBookingConfirmationService
{
    Task<BookingConfirmationResult> ConfirmBookingAfterPaymentAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
