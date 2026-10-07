namespace EventPulse.BookingService.DTOs;

public record CustomerBookingHistoryDto(
    Guid Id,
    string BookingReference,
    Guid EventId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset ExpiresAt,
    int TotalTickets,
    IReadOnlyList<BookingHistoryItemDto> Items,
    string? EventName = null,
    DateTime? EventDate = null
)
{
    public string? EventTitle => EventName;
}

public record BookingHistoryItemDto(
    Guid TicketTypeId,
    string TicketName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);
