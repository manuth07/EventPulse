using System.Security.Claims;
using EventPulse.Contracts.Kafka;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.DTOs;
using EventPulse.PaymentService.Events;
using EventPulse.PaymentService.Models;
using EventPulse.PaymentService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace EventPulse.PaymentService.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly PaymentDbContext _dbContext;
    private readonly IBookingServiceClient _bookingClient;
    private readonly IStripeCheckoutService _stripeService;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        PaymentDbContext dbContext,
        IBookingServiceClient bookingClient,
        IStripeCheckoutService stripeService,
        IOutboxWriter outboxWriter,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _dbContext = dbContext;
        _bookingClient = bookingClient;
        _stripeService = stripeService;
        _outboxWriter = outboxWriter;
        _configuration = configuration;
        _logger = logger;
    }

    private bool TryGetCustomerId(out Guid customerId)
    {
        var customerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(customerIdStr, out customerId);
    }

    private string? GetBearerToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }
        return string.IsNullOrWhiteSpace(authHeader) ? null : authHeader.Trim();
    }

    [HttpPost("checkout-session")]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.BookingId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_REQUEST", message = "Invalid booking ID." });
        }

        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var token = GetBearerToken();
        var bookingSummary = await _bookingClient.GetBookingSummaryAsync(request.BookingId, token, cancellationToken);

        if (bookingSummary == null)
        {
            return NotFound(new { code = "BOOKING_NOT_FOUND", message = "Booking could not be found." });
        }

        if (bookingSummary.CustomerId != customerId)
        {
            _logger.LogWarning("Customer {CustomerId} attempted to pay for booking {BookingId} owned by {OwnerId}",
                customerId, bookingSummary.Id, bookingSummary.CustomerId);
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "FORBIDDEN",
                message = "You are not authorized to initiate payment for this booking."
            });
        }

        if (!string.Equals(bookingSummary.Status, "PendingPayment", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                code = "INVALID_BOOKING_STATUS",
                message = $"Booking status is '{bookingSummary.Status}'. Only 'PendingPayment' bookings can be paid."
            });
        }

        var payment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingSummary.Id && p.Status == PaymentStatus.Pending, cancellationToken);

        if (payment == null)
        {
            payment = new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = bookingSummary.Id,
                BookingReference = bookingSummary.BookingReference,
                CustomerId = customerId,
                Amount = bookingSummary.TotalAmount,
                Currency = "lkr",
                Status = PaymentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.Payments.Add(payment);
        }
        else
        {
            payment.Amount = bookingSummary.TotalAmount;
            payment.BookingReference = bookingSummary.BookingReference;
            payment.CustomerId = customerId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var sessionResult = await _stripeService.CreateSessionAsync(payment, cancellationToken);

            payment.StripeSessionId = sessionResult.SessionId;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new CheckoutSessionResponse
            {
                PaymentId = payment.Id,
                BookingId = payment.BookingId,
                BookingReference = payment.BookingReference,
                SessionId = sessionResult.SessionId,
                CheckoutUrl = sessionResult.CheckoutUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Stripe Checkout Session for Payment {PaymentId}", payment.Id);
            payment.Status = PaymentStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                code = "STRIPE_SESSION_ERROR",
                message = "Failed to initialize Stripe checkout session. Please try again later."
            });
        }
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        string json;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        var webhookSecret = _configuration["Stripe:WebhookSecret"]
            ?? _configuration["STRIPE_WEBHOOK_SECRET"]
            ?? _configuration["Stripe__WebhookSecret"];

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature validation failed.");
            return BadRequest(new { message = "Invalid Stripe Signature" });
        }

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
        {
            if (stripeEvent.Data.Object is Stripe.Checkout.Session session)
            {
                Guid.TryParse(session.Metadata?.GetValueOrDefault("PaymentId"), out var paymentIdFromMeta);

                var payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.StripeSessionId == session.Id || (paymentIdFromMeta != Guid.Empty && p.Id == paymentIdFromMeta), cancellationToken);

                if (payment == null)
                {
                    _logger.LogWarning("Payment record not found for Stripe checkout session {SessionId}", session.Id);
                    return Ok();
                }

                if (payment.Status == PaymentStatus.Succeeded)
                {
                    _logger.LogInformation("Payment {PaymentId} already marked Succeeded. Skipping.", payment.Id);
                    return Ok();
                }

                payment.Status = PaymentStatus.Succeeded;
                payment.StripePaymentIntentId = session.PaymentIntentId ?? payment.StripePaymentIntentId;
                payment.CompletedAt = DateTimeOffset.UtcNow;

                var eventId = Guid.NewGuid();
                var evt = new PaymentSucceededEvent
                {
                    EventId = eventId,
                    EventVersion = 1,
                    OccurredAtUtc = payment.CompletedAt ?? DateTimeOffset.UtcNow,
                    PaymentId = payment.Id,
                    BookingId = payment.BookingId,
                    BookingReference = payment.BookingReference,
                    CustomerId = payment.CustomerId,
                    Amount = payment.Amount,
                    Currency = payment.Currency
                };

                _outboxWriter.EnqueuePaymentSucceeded(evt);

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else if (stripeEvent.Type == EventTypes.PaymentIntentPaymentFailed)
        {
            if (stripeEvent.Data.Object is Stripe.PaymentIntent intent)
            {
                var failureReason = intent.LastPaymentError?.Message ?? "Payment declined";
                Guid.TryParse(intent.Metadata?.GetValueOrDefault("PaymentId"), out var paymentIdFromMeta);

                var payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => (p.StripePaymentIntentId != null && p.StripePaymentIntentId == intent.Id) || (paymentIdFromMeta != Guid.Empty && p.Id == paymentIdFromMeta), cancellationToken);

                if (payment == null)
                {
                    _logger.LogWarning("Payment record not found for Stripe PaymentIntent {PaymentIntentId}", intent.Id);
                    return Ok();
                }

                if (payment.Status == PaymentStatus.Failed)
                {
                    _logger.LogInformation("Payment {PaymentId} already marked Failed. Skipping.", payment.Id);
                    return Ok();
                }

                payment.Status = PaymentStatus.Failed;

                var eventId = Guid.NewGuid();
                var evt = new PaymentFailedEvent
                {
                    EventId = eventId,
                    EventVersion = 1,
                    OccurredAtUtc = DateTimeOffset.UtcNow,
                    PaymentId = payment.Id,
                    BookingId = payment.BookingId,
                    BookingReference = payment.BookingReference,
                    CustomerId = payment.CustomerId,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                    FailureCode = intent.LastPaymentError?.Code,
                    FailureReason = failureReason
                };

                _outboxWriter.EnqueuePaymentFailed(evt);

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok();
    }
}
