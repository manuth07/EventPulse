namespace EventPulse.BookingService.DTOs;

public class CancelBookingRequest
{
    public string? Reason { get; set; }
}

public record BookingCancellationResultDto
{
    public Guid BookingId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public string PreviousStatus { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public DateTimeOffset CancelledAt { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }

    public BookingCancellationResultDto() { }

    public BookingCancellationResultDto(
        Guid bookingId,
        string bookingReference,
        string previousStatus,
        string newStatus,
        DateTimeOffset cancelledAt,
        bool success,
        string? message = null)
    {
        BookingId = bookingId;
        BookingReference = bookingReference;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        CancelledAt = cancelledAt;
        Success = success;
        Message = message;
    }
}
