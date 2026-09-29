namespace EventPulse.BookingService.DTOs;

public class CancelTicketRequest
{
    public string? Reason { get; set; }
}

public record TicketCancellationResultDto(
    Guid TicketId,
    string TicketCode,
    string Status,
    string ParentBookingStatus,
    bool Success,
    string? Message = null
);
