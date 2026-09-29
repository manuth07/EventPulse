using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

public class BookingCancellationService : IBookingCancellationService
{
    private readonly BookingDbContext _dbContext;
    private readonly ILogger<BookingCancellationService> _logger;

    public BookingCancellationService(BookingDbContext dbContext, ILogger<BookingCancellationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<(bool IsEligible, string? Reason, Booking? Booking)> EvaluateCancellationEligibilityAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken ct = default)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.Items)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null || booking.CustomerId != customerId)
        {
            _logger.LogWarning("Cancellation eligibility failed for BookingId {BookingId}, CustomerId {CustomerId}: Booking not found or access denied.", bookingId, customerId);
            return (false, "Booking not found or access denied", null);
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            _logger.LogInformation("Cancellation eligibility failed for BookingId {BookingId}: Booking is already cancelled.", bookingId);
            return (false, "Booking is already cancelled", booking);
        }

        if (booking.Status == BookingStatus.PaymentFailed)
        {
            _logger.LogInformation("Cancellation eligibility failed for BookingId {BookingId}: Cannot cancel a failed payment booking.", bookingId);
            return (false, "Cannot cancel a failed payment booking", booking);
        }

        return (true, null, booking);
    }

    public async Task<BookingCancellationResultDto> CancelBookingAsync(
        Guid bookingId,
        Guid customerId,
        CancelBookingRequest? request,
        CancellationToken ct = default)
    {
        var (isEligible, reason, booking) = await EvaluateCancellationEligibilityAsync(bookingId, customerId, ct);

        if (!isEligible || booking == null)
        {
            return new BookingCancellationResultDto(
                bookingId: bookingId,
                bookingReference: booking?.BookingReference ?? string.Empty,
                previousStatus: booking?.Status.ToString() ?? string.Empty,
                newStatus: booking?.Status.ToString() ?? string.Empty,
                cancelledAt: DateTimeOffset.UtcNow,
                success: false,
                message: reason ?? "Booking is not eligible for cancellation"
            );
        }

        var previousStatus = booking.Status.ToString();
        booking.Status = BookingStatus.Cancelled;

        if (booking.Tickets != null)
        {
            foreach (var ticket in booking.Tickets)
            {
                ticket.Status = TicketStatus.Cancelled;
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Booking {BookingId} ({BookingReference}) cancelled successfully by customer {CustomerId}. Reason: {Reason}",
            booking.Id, booking.BookingReference, customerId, request?.Reason ?? "None");

        return new BookingCancellationResultDto(
            bookingId: booking.Id,
            bookingReference: booking.BookingReference,
            previousStatus: previousStatus,
            newStatus: BookingStatus.Cancelled.ToString(),
            cancelledAt: DateTimeOffset.UtcNow,
            success: true,
            message: "Booking cancelled successfully"
        );
    }
}
