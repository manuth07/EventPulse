namespace EventPulse.BookingService.Models;

public class Ticket
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid BookingItemId { get; set; }
    public Guid TicketTypeId { get; set; }
    public string TicketName { get; set; } = string.Empty;
    public int TicketSequence { get; set; }
    public Guid EventId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string ValidationToken { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Valid;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Booking? Booking { get; set; }
    public BookingItem? BookingItem { get; set; }
}
