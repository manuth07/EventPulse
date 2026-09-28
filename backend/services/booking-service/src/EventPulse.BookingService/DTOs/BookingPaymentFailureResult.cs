using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.DTOs;

public enum BookingPaymentFailureStatus
{
    Failed,
    AlreadyFailed,
    AlreadyConfirmed,
    Cancelled,
    NotFound
}

public class BookingPaymentFailureResult
{
    public BookingPaymentFailureStatus Status { get; }
    public Guid BookingId { get; }
    public Booking? Booking { get; }
    public string? Message { get; }

    private BookingPaymentFailureResult(
        BookingPaymentFailureStatus status,
        Guid bookingId,
        Booking? booking,
        string? message = null)
    {
        Status = status;
        BookingId = bookingId;
        Booking = booking;
        Message = message;
    }

    public static BookingPaymentFailureResult Failed(Booking booking) =>
        new(BookingPaymentFailureStatus.Failed, booking.Id, booking, "Booking marked as PaymentFailed.");

    public static BookingPaymentFailureResult AlreadyFailed(Booking booking) =>
        new(BookingPaymentFailureStatus.AlreadyFailed, booking.Id, booking, "Booking is already in PaymentFailed status.");

    public static BookingPaymentFailureResult AlreadyConfirmed(Booking booking) =>
        new(BookingPaymentFailureStatus.AlreadyConfirmed, booking.Id, booking, "Booking is already Confirmed; late failure event safely ignored.");

    public static BookingPaymentFailureResult Cancelled(Booking booking) =>
        new(BookingPaymentFailureStatus.Cancelled, booking.Id, booking, "Booking is Cancelled; failure event safely ignored.");

    public static BookingPaymentFailureResult NotFound(Guid bookingId) =>
        new(BookingPaymentFailureStatus.NotFound, bookingId, null, $"Booking {bookingId} not found.");
}
