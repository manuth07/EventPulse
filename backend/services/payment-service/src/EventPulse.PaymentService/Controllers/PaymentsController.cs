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

        if (string.IsNullOrWhiteSpace(bookingSummary.BookingReference))
        {
            return BadRequest(new
            {
                code = "MISSING_BOOKING_REFERENCE",
                message = "Booking reference is missing or invalid."
            });
        }

        if (bookingSummary.Id == Guid.Empty)
        {
            return BadRequest(new
            {
                code = "INVALID_BOOKING_ID",
                message = "Booking ID is missing or invalid."
            });
        }

        if (!string.Equals(bookingSummary.Status, "PendingPayment", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(bookingSummary.Status, "PaymentFailed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                code = "INVALID_BOOKING_STATUS",
                message = $"Booking status is '{bookingSummary.Status}'. Only 'PendingPayment' or 'PaymentFailed' bookings can be paid."
            });
        }

        var existingSucceededPayment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.BookingId == bookingSummary.Id && p.Status == PaymentStatus.Succeeded, cancellationToken);

        if (existingSucceededPayment != null)
        {
            return BadRequest(new
            {
                code = "BOOKING_ALREADY_PAID",
                message = "A successful payment already exists for this booking."
            });
        }

        var payment = await _dbContext.Payments
            .OrderByDescending(p => p.CreatedAt)
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
            payment.CompletedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                code = "STRIPE_SESSION_ERROR",
                message = "Failed to initialize Stripe checkout session. Please try again later."
            });
        }
    }

    [HttpGet("by-booking/{bookingId:guid}")]
    public async Task<IActionResult> GetPaymentsByBooking([FromRoute] Guid bookingId, CancellationToken cancellationToken)
    {
        if (bookingId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_BOOKING_ID", message = "Invalid booking ID." });
        }

        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var payments = await _dbContext.Payments
            .Where(p => p.BookingId == bookingId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0)
        {
            return NotFound(new { code = "PAYMENT_NOT_FOUND", message = "No payment records found for this booking." });
        }

        if (payments.Any(p => p.CustomerId != customerId) && !User.IsInRole("Administrator"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "FORBIDDEN",
                message = "You are not authorized to view payments for this booking."
            });
        }

        var result = payments.Select(p => new PaymentDto
        {
            Id = p.Id,
            BookingId = p.BookingId,
            BookingReference = p.BookingReference,
            CustomerId = p.CustomerId,
            Amount = p.Amount,
            Currency = p.Currency,
            Status = p.Status.ToString(),
            StripeSessionId = p.StripeSessionId,
            StripePaymentIntentId = p.StripePaymentIntentId,
            CreatedAt = p.CreatedAt,
            CompletedAt = p.CompletedAt
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Retrieves the authenticated customer's paginated payment history (EP-356 / EP-54).
    /// </summary>
    [HttpGet("my-payments")]
    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedResult<CustomerPaymentHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyPayments(
        [FromQuery] PaymentHistoryQueryParameters queryParams,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid or missing token identity." });
        }

        var query = _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(queryParams.Status))
        {
            if (Enum.TryParse<PaymentStatus>(queryParams.Status, ignoreCase: true, out var statusEnum))
            {
                query = query.Where(p => p.Status == statusEnum);
            }
            else
            {
                _logger.LogWarning("Invalid payment status filter supplied: {Status}", queryParams.Status);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync(cancellationToken);

        var token = GetBearerToken();

        // Resolve event names where available through existing supported contracts
        var distinctBookingIds = payments.Select(p => p.BookingId).Distinct().ToList();
        var eventNamesByBooking = new Dictionary<Guid, string?>();

        if (distinctBookingIds.Count > 0)
        {
            var eventNameTasks = distinctBookingIds.Select(async bId =>
            {
                var eventName = await _bookingClient.GetBookingEventNameAsync(bId, token, cancellationToken);
                return (bId, eventName);
            });

            var eventNameResults = await Task.WhenAll(eventNameTasks);
            foreach (var (bId, eventName) in eventNameResults)
            {
                eventNamesByBooking[bId] = eventName;
            }
        }

        var items = payments.Select(p => new CustomerPaymentHistoryItemDto
        {
            Id = p.Id,
            BookingId = p.BookingId,
            BookingReference = p.BookingReference,
            Amount = p.Amount,
            Currency = p.Currency,
            Status = p.Status.ToString(),
            PaymentDate = p.CompletedAt ?? p.CreatedAt,
            CreatedAt = p.CreatedAt,
            CompletedAt = p.CompletedAt,
            EventName = eventNamesByBooking.GetValueOrDefault(p.BookingId)
        }).ToList();

        var pagedResult = new PagedResult<CustomerPaymentHistoryItemDto>(items, totalCount, queryParams.Page, queryParams.PageSize);
        return Ok(pagedResult);
    }

    /// <summary>
    /// Retrieves a single payment detail record for the authenticated owner (EP-356 / EP-54).
    /// </summary>
    [HttpGet("{paymentId:guid}")]
    [ProducesResponseType(typeof(CustomerPaymentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentById([FromRoute] Guid paymentId, CancellationToken cancellationToken)
    {
        if (paymentId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_PAYMENT_ID", message = "Invalid payment ID." });
        }

        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        var payment = await _dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        // Security: Return 404 for inaccessible records to avoid revealing their existence to non-owners
        if (payment == null || (payment.CustomerId != customerId && !User.IsInRole("Administrator")))
        {
            return NotFound(new { code = "PAYMENT_NOT_FOUND", message = "Payment not found." });
        }

        var token = GetBearerToken();
        var eventName = await _bookingClient.GetBookingEventNameAsync(payment.BookingId, token, cancellationToken);

        return Ok(new CustomerPaymentDetailDto
        {
            Id = payment.Id,
            BookingId = payment.BookingId,
            BookingReference = payment.BookingReference,
            CustomerId = payment.CustomerId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status.ToString(),
            PaymentDate = payment.CompletedAt ?? payment.CreatedAt,
            CreatedAt = payment.CreatedAt,
            CompletedAt = payment.CompletedAt,
            EventName = eventName,
            StripeSessionId = payment.StripeSessionId,
            StripePaymentIntentId = payment.StripePaymentIntentId
        });
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
                Guid.TryParse(session.Metadata?.GetValueOrDefault("BookingId"), out var bookingIdFromMeta);

                var payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.StripeSessionId == session.Id || (paymentIdFromMeta != Guid.Empty && p.Id == paymentIdFromMeta), cancellationToken);

                // Fallback: If not found by SessionId or PaymentId, find pending payment by BookingId from metadata
                if (payment == null && bookingIdFromMeta != Guid.Empty)
                {
                    payment = await _dbContext.Payments
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync(p => p.BookingId == bookingIdFromMeta && p.Status == PaymentStatus.Pending, cancellationToken);
                }

                if (payment == null)
                {
                    _logger.LogWarning("Payment record not found for Stripe checkout session {SessionId}", session.Id);
                    return Ok();
                }

                // Repair/ensure booking reference association from metadata if missing
                if (string.IsNullOrWhiteSpace(payment.BookingReference) && session.Metadata != null && session.Metadata.TryGetValue("BookingReference", out var metaRef) && !string.IsNullOrWhiteSpace(metaRef))
                {
                    payment.BookingReference = metaRef;
                }

                if (payment.BookingId == Guid.Empty && bookingIdFromMeta != Guid.Empty)
                {
                    payment.BookingId = bookingIdFromMeta;
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
                Guid.TryParse(intent.Metadata?.GetValueOrDefault("BookingId"), out var bookingIdFromMeta);

                var payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => (p.StripePaymentIntentId != null && p.StripePaymentIntentId == intent.Id) || (paymentIdFromMeta != Guid.Empty && p.Id == paymentIdFromMeta), cancellationToken);

                // Fallback: If not found by PaymentIntentId or PaymentId, find pending payment by BookingId from metadata
                if (payment == null && bookingIdFromMeta != Guid.Empty)
                {
                    payment = await _dbContext.Payments
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync(p => p.BookingId == bookingIdFromMeta && p.Status == PaymentStatus.Pending, cancellationToken);
                }

                if (payment == null)
                {
                    _logger.LogWarning("Payment record not found for Stripe PaymentIntent {PaymentIntentId}", intent.Id);
                    return Ok();
                }

                // Repair/ensure booking reference association from metadata if missing
                if (string.IsNullOrWhiteSpace(payment.BookingReference) && intent.Metadata != null && intent.Metadata.TryGetValue("BookingReference", out var metaRef) && !string.IsNullOrWhiteSpace(metaRef))
                {
                    payment.BookingReference = metaRef;
                }

                if (payment.BookingId == Guid.Empty && bookingIdFromMeta != Guid.Empty)
                {
                    payment.BookingId = bookingIdFromMeta;
                }

                if (payment.Status == PaymentStatus.Failed)
                {
                    _logger.LogInformation("Payment {PaymentId} already marked Failed. Skipping.", payment.Id);
                    return Ok();
                }

                payment.StripePaymentIntentId = intent.Id ?? payment.StripePaymentIntentId;
                payment.Status = PaymentStatus.Failed;
                payment.CompletedAt = DateTimeOffset.UtcNow;

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
