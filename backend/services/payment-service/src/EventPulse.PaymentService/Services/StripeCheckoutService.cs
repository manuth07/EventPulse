using EventPulse.PaymentService.Models;
using Stripe.Checkout;

namespace EventPulse.PaymentService.Services;

public class StripeCheckoutService : IStripeCheckoutService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeCheckoutService> _logger;

    public StripeCheckoutService(IConfiguration configuration, ILogger<StripeCheckoutService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public SessionCreateOptions BuildSessionOptions(Payment payment)
    {
        var currency = payment.Currency.ToLowerInvariant();
        var successUrl = _configuration["Stripe:SuccessUrl"] ?? "http://localhost:5173/payment-success?session_id={CHECKOUT_SESSION_ID}";
        var cancelUrl = _configuration["Stripe:CancelUrl"] ?? "http://localhost:5173/payment-cancel";

        var unitAmount = Convert.ToInt64(payment.Amount * 100);

        return new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = unitAmount,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Booking {payment.BookingReference}"
                        }
                    },
                    Quantity = 1
                }
            },
            Metadata = new Dictionary<string, string>
            {
                { "BookingId", payment.BookingId.ToString() },
                { "BookingReference", payment.BookingReference },
                { "PaymentId", payment.Id.ToString() }
            }
        };
    }

    public async Task<StripeSessionResult> CreateSessionAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var options = BuildSessionOptions(payment);
        var unitAmount = options.LineItems[0].PriceData.UnitAmount;
        var service = new SessionService();
        
        _logger.LogInformation("Creating Stripe session for Booking {Ref}: Amount {Amount} {Currency} ({UnitAmount} minor units)",
            payment.BookingReference, payment.Amount, payment.Currency, unitAmount);

        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return new StripeSessionResult(session.Id, session.Url);
    }
}
