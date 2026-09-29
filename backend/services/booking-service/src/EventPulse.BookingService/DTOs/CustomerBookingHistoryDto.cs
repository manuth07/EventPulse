namespace EventPulse.BookingService.DTOs;

public record CustomerBookingHistoryDto(
    Guid Id,
    string BookingReference,
    Guid EventId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    int TotalTickets,
    IReadOnlyList<BookingHistoryItemDto> Items
);

public record BookingHistoryItemDto(
    Guid TicketTypeId,
    string TicketName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);
