namespace EventPulse.PaymentService.Models;

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3,
    PartiallyRefunded = 4
}
