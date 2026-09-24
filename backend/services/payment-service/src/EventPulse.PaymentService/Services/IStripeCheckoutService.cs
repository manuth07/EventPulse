using EventPulse.PaymentService.Models;
using Stripe.Checkout;

namespace EventPulse.PaymentService.Services;

public record StripeSessionResult(
    string SessionId,
    string CheckoutUrl
);

public interface IStripeCheckoutService
{
    SessionCreateOptions BuildSessionOptions(Payment payment);
    Task<StripeSessionResult> CreateSessionAsync(Payment payment, CancellationToken cancellationToken = default);
}
