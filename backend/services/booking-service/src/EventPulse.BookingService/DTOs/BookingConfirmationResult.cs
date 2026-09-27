using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.DTOs;

public enum BookingConfirmationStatus
{
    Confirmed,
    AlreadyConfirmed,
    NotFound,
    InvalidState
}

public class BookingConfirmationResult
{
    public BookingConfirmationStatus Status { get; init; }
    public Guid BookingId { get; init; }
    public Booking? Booking { get; init; }
    public string? Message { get; init; }
    public bool IsSuccess => Status is BookingConfirmationStatus.Confirmed or BookingConfirmationStatus.AlreadyConfirmed;

    public static BookingConfirmationResult Confirmed(Booking booking) => new()
    {
        Status = BookingConfirmationStatus.Confirmed,
        BookingId = booking.Id,
        Booking = booking,
        Message = $"Booking {booking.BookingReference} successfully confirmed."
    };

    public static BookingConfirmationResult AlreadyConfirmed(Booking booking) => new()
    {
        Status = BookingConfirmationStatus.AlreadyConfirmed,
        BookingId = booking.Id,
        Booking = booking,
        Message = $"Booking {booking.BookingReference} is already confirmed."
    };

    public static BookingConfirmationResult NotFound(Guid bookingId) => new()
    {
        Status = BookingConfirmationStatus.NotFound,
        BookingId = bookingId,
        Message = $"Booking {bookingId} not found."
    };

    public static BookingConfirmationResult InvalidState(Guid bookingId, BookingStatus currentStatus, string message) => new()
    {
        Status = BookingConfirmationStatus.InvalidState,
        BookingId = bookingId,
        Message = message
    };
}
