namespace EventPulse.PaymentService.Services;

/// <summary>
/// Result of a Stripe refund operation (EP-365).
/// </summary>
public record StripeRefundResult(
    bool Success,
    string? StripeRefundId,
    string? Status,
    string? FailureReason,
    bool IsMockOrSimulated = false
);

/// <summary>
/// Abstraction for Stripe refund operations, ensuring idempotency, failure recovery, and decoupling from live keys (EP-365).
/// </summary>
public interface IStripeRefundService
{
    Task<StripeRefundResult> CreateRefundAsync(
        string paymentIntentOrSessionId,
        decimal amount,
        string currency,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
