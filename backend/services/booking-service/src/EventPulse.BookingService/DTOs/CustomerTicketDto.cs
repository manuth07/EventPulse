namespace EventPulse.BookingService.DTOs;

public class CustomerTicketDto
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string? EventName { get; set; }
    public string? EventTitle => EventName;
    public DateTime? EventDate { get; set; }
    public string? EventVenue { get; set; }
    public Guid TicketTypeId { get; set; }
    public string TicketName { get; set; } = string.Empty;
    public int TicketSequence { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ValidationToken { get; set; } = string.Empty;
    public string QrPayload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class CustomerBookingTicketsResponseDto
{
    public Guid BookingId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string? EventName { get; set; }
    public string? EventTitle => EventName;
    public DateTime? EventDate { get; set; }
    public string? EventVenue { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public IReadOnlyList<CustomerTicketDto> Tickets { get; set; } = Array.Empty<CustomerTicketDto>();
}
