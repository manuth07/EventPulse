using Stripe;

namespace EventPulse.PaymentService.Services;

/// <summary>
/// Implements Stripe refund operations using Stripe.NET with idempotency keys and error resilience (EP-365).
/// </summary>
public class StripeRefundService : IStripeRefundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeRefundService> _logger;

    public StripeRefundService(IConfiguration configuration, ILogger<StripeRefundService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<StripeRefundResult> CreateRefundAsync(
        string paymentIntentOrSessionId,
        decimal amount,
        string currency,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var secretKey = _configuration["STRIPE_SECRET_KEY"]
            ?? _configuration["Stripe__SecretKey"]
            ?? _configuration["Stripe:SecretKey"]
            ?? _configuration.GetSection("Stripe")["SecretKey"];

        // If secret key is not configured, is a test placeholder, or no payment intent exists:
        // Safely simulate refund eligibility without claiming money was returned by Stripe.
        if (string.IsNullOrWhiteSpace(secretKey) ||
            secretKey.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(paymentIntentOrSessionId) ||
            !paymentIntentOrSessionId.StartsWith("pi_", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Stripe live refund bypassed (Simulated eligibility). PaymentIntent: '{PaymentIntent}', Amount: {Amount} {Currency}, IdempotencyKey: {Key}",
                paymentIntentOrSessionId, amount, currency, idempotencyKey);

            return new StripeRefundResult(
                Success: true,
                StripeRefundId: $"sim_re_{Guid.NewGuid():N}",
                Status: "simulated_eligible",
                FailureReason: null,
                IsMockOrSimulated: true
            );
        }

        try
        {
            var unitAmount = Convert.ToInt64(amount * 100);
            var options = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentOrSessionId,
                Amount = unitAmount,
                Reason = reason.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                    ? RefundReasons.Duplicate
                    : RefundReasons.RequestedByCustomer
            };

            var requestOptions = new RequestOptions
            {
                IdempotencyKey = idempotencyKey
            };

            var service = new Stripe.RefundService();
            _logger.LogInformation(
                "Calling Stripe RefundService for PaymentIntent {PaymentIntent}, Amount {Amount} ({UnitAmount} minor units), IdempotencyKey: {Key}",
                paymentIntentOrSessionId, amount, unitAmount, idempotencyKey);

            var refund = await service.CreateAsync(options, requestOptions, cancellationToken);

            var isSuccessful = string.Equals(refund.Status, "succeeded", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(refund.Status, "pending", StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Stripe refund response: ID={RefundId}, Status={Status}, FailureReason={FailureReason}",
                refund.Id, refund.Status, refund.FailureReason);

            return new StripeRefundResult(
                Success: isSuccessful,
                StripeRefundId: refund.Id,
                Status: refund.Status,
                FailureReason: refund.FailureReason,
                IsMockOrSimulated: false
            );
        }
        catch (StripeException ex)
        {
            var errorMsg = ex.StripeError?.Message ?? ex.Message;
            _logger.LogError(ex, "Stripe refund failed for PaymentIntent {PaymentIntent}: {Error} (Code: {Code})",
                paymentIntentOrSessionId, errorMsg, ex.StripeError?.Code);

            return new StripeRefundResult(
                Success: false,
                StripeRefundId: null,
                Status: "failed",
                FailureReason: errorMsg,
                IsMockOrSimulated: false
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error calling Stripe refund for PaymentIntent {PaymentIntent}", paymentIntentOrSessionId);

            return new StripeRefundResult(
                Success: false,
                StripeRefundId: null,
                Status: "failed",
                FailureReason: ex.Message,
                IsMockOrSimulated: false
            );
        }
    }
}
