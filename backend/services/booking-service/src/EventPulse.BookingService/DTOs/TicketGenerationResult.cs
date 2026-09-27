using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.DTOs;

public enum TicketGenerationStatus
{
    Success,
    AlreadyGenerated,
    InvalidStatus,
    NotFound
}

public class TicketGenerationResult
{
    public TicketGenerationStatus Status { get; init; }
    public Guid BookingId { get; init; }
    public IReadOnlyList<Ticket> Tickets { get; init; } = Array.Empty<Ticket>();
    public int GeneratedCount { get; init; }
    public int TotalTicketCount { get; init; }
    public string? Message { get; init; }
    public bool IsSuccess => Status is TicketGenerationStatus.Success or TicketGenerationStatus.AlreadyGenerated;

    public static TicketGenerationResult Success(Guid bookingId, IReadOnlyList<Ticket> tickets, int newlyGeneratedCount) => new()
    {
        Status = newlyGeneratedCount > 0 ? TicketGenerationStatus.Success : TicketGenerationStatus.AlreadyGenerated,
        BookingId = bookingId,
        Tickets = tickets,
        GeneratedCount = newlyGeneratedCount,
        TotalTicketCount = tickets.Count,
        Message = newlyGeneratedCount > 0
            ? $"Successfully generated {newlyGeneratedCount} ticket(s) for booking {bookingId}."
            : $"All {tickets.Count} ticket(s) for booking {bookingId} were already generated."
    };

    public static TicketGenerationResult NotFound(Guid bookingId) => new()
    {
        Status = TicketGenerationStatus.NotFound,
        BookingId = bookingId,
        Message = $"Booking {bookingId} was not found."
    };

    public static TicketGenerationResult InvalidStatus(Guid bookingId, BookingStatus currentStatus) => new()
    {
        Status = TicketGenerationStatus.InvalidStatus,
        BookingId = bookingId,
        Message = $"Cannot generate tickets for booking {bookingId} with status '{currentStatus}'. Tickets can only be generated for Confirmed bookings."
    };
}
