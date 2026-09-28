using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

public class BookingPaymentFailureService : IBookingPaymentFailureService
{
    private readonly BookingDbContext _dbContext;
    private readonly ILogger<BookingPaymentFailureService> _logger;

    public BookingPaymentFailureService(
        BookingDbContext dbContext,
        ILogger<BookingPaymentFailureService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<BookingPaymentFailureResult> HandlePaymentFailureAsync(
        Guid bookingId,
        string? failureCode = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling payment failure for Booking {BookingId} (Code: {FailureCode}, Reason: {FailureReason})",
            bookingId, failureCode ?? "N/A", failureReason ?? "N/A");

        var booking = await _dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            _logger.LogWarning("Booking {BookingId} not found when handling payment failure", bookingId);
            return BookingPaymentFailureResult.NotFound(bookingId);
        }

        if (booking.Status == BookingStatus.Confirmed)
        {
            _logger.LogWarning(
                "Stale or out-of-order failure event received for Booking {BookingId}. Booking is already Confirmed; preserving Confirmed status",
                bookingId);
            return BookingPaymentFailureResult.AlreadyConfirmed(booking);
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            _logger.LogInformation(
                "Failure event received for Booking {BookingId}, but booking is Cancelled; preserving Cancelled status",
                bookingId);
            return BookingPaymentFailureResult.Cancelled(booking);
        }

        if (booking.Status == BookingStatus.PaymentFailed)
        {
            _logger.LogInformation(
                "Booking {BookingId} is already in PaymentFailed status; treating operation as idempotent",
                bookingId);
            return BookingPaymentFailureResult.AlreadyFailed(booking);
        }

        if (booking.Status == BookingStatus.PendingPayment)
        {
            booking.Status = BookingStatus.PaymentFailed;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Booking {BookingId} transitioned from PendingPayment to PaymentFailed (Reference: {Ref})",
                booking.Id, booking.BookingReference);

            return BookingPaymentFailureResult.Failed(booking);
        }

        _logger.LogWarning(
            "Booking {BookingId} has unhandled status {Status} during payment failure transition",
            bookingId, booking.Status);

        return BookingPaymentFailureResult.AlreadyFailed(booking);
    }
}
