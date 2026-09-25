namespace EventPulse.PaymentService.DTOs;

public class BookingSummaryDto
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;

    public BookingSummaryDto() { }

    public BookingSummaryDto(Guid id, string bookingReference, Guid customerId, decimal totalAmount, string status)
    {
        Id = id;
        BookingReference = bookingReference;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        Status = status;
    }
}
