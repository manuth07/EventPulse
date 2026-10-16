namespace EventPulse.BookingService.DTOs;

public class CustomerBookingDetailDto
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid EventId { get; set; }
    public string? EventName { get; set; }
    public string? EventTitle => EventName;
    public DateTime? EventDate { get; set; }
    public string? EventVenue { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int TotalTickets { get; set; }
    public IReadOnlyList<BookingHistoryItemDto> Items { get; set; } = Array.Empty<BookingHistoryItemDto>();
    public IReadOnlyList<CustomerTicketDto> Tickets { get; set; } = Array.Empty<CustomerTicketDto>();
}

public class CustomerBookingDetailResult
{
    public bool Found { get; set; }
    public bool IsAuthorized { get; set; }
    public CustomerBookingDetailDto? Booking { get; set; }

    public static CustomerBookingDetailResult NotFound() => new() { Found = false, IsAuthorized = true };
    public static CustomerBookingDetailResult Forbidden() => new() { Found = true, IsAuthorized = false };
    public static CustomerBookingDetailResult Success(CustomerBookingDetailDto booking) => new() { Found = true, IsAuthorized = true, Booking = booking };
}
