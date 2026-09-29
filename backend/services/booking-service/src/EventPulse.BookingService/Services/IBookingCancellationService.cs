using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

public interface IBookingCancellationService
{
    Task<(bool IsEligible, string? Reason, Booking? Booking)> EvaluateCancellationEligibilityAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken ct = default);

    Task<BookingCancellationResultDto> CancelBookingAsync(
        Guid bookingId,
        Guid customerId,
        CancelBookingRequest? request,
        CancellationToken ct = default);

    Task<TicketCancellationResultDto> CancelSingleTicketAsync(
        Guid ticketId, 
        Guid customerId, 
        string? reason = default, 
        CancellationToken ct = default);
}
