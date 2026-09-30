namespace EventPulse.BookingService.Models;

public enum BookingStatus
{
    PendingPayment = 1,
    Confirmed = 2,
    PaymentFailed = 3,
    Cancelled = 4
}
