using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

public class BookingConfirmationService : IBookingConfirmationService
{
    private readonly BookingDbContext _dbContext;
    private readonly ILogger<BookingConfirmationService> _logger;

    public BookingConfirmationService(
        BookingDbContext dbContext,
        ILogger<BookingConfirmationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<BookingConfirmationResult> ConfirmBookingAfterPaymentAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Confirming Booking {BookingId} after successful payment", bookingId);

        var booking = await _dbContext.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            _logger.LogWarning("Booking {BookingId} not found for confirmation", bookingId);
            return BookingConfirmationResult.NotFound(bookingId);
        }

        if (booking.Status == BookingStatus.Confirmed)
        {
            _logger.LogInformation("Booking {BookingId} is already confirmed; treating operation as idempotent", bookingId);
            return BookingConfirmationResult.AlreadyConfirmed(booking);
        }

        if (booking.Status != BookingStatus.PendingPayment && booking.Status != BookingStatus.PaymentFailed)
        {
            _logger.LogWarning("Cannot confirm Booking {BookingId} from status {Status}", bookingId, booking.Status);
            return BookingConfirmationResult.InvalidState(
                bookingId,
                booking.Status,
                $"Cannot confirm booking {booking.BookingReference} from status '{booking.Status}'.");
        }

        var previousStatus = booking.Status;
        booking.Status = BookingStatus.Confirmed;
        booking.ConfirmedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Booking {BookingId} confirmed (transitioned from {PreviousStatus})", booking.Id, previousStatus);

        return BookingConfirmationResult.Confirmed(booking);
    }
}
