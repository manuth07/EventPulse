using EventPulse.BookingService.DTOs;

namespace EventPulse.BookingService.Services;

public interface ITicketGenerationService
{
    Task<TicketGenerationResult> GenerateTicketsForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
