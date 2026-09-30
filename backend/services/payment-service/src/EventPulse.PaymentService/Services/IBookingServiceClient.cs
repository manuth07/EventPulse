using EventPulse.PaymentService.DTOs;

namespace EventPulse.PaymentService.Services;

public interface IBookingServiceClient
{
    Task<BookingSummaryDto?> GetBookingSummaryAsync(Guid bookingId, string? bearerToken = null, CancellationToken cancellationToken = default);
}
