namespace EventPulse.PaymentService.Models;

/// <summary>
/// Status of an individual refund operation (EP-365).
/// </summary>
public enum RefundStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    NotRefundable = 3
}
