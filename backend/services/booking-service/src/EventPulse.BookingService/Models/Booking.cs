namespace EventPulse.BookingService.Models;

public class Booking
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid EventId { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmedAt { get; set; }

    public ICollection<BookingItem> Items { get; set; } = new List<BookingItem>();
}
