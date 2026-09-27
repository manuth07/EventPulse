using System.Security.Claims;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.DTOs;
using EventPulse.PaymentService.Models;
using EventPulse.PaymentService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventPulse.PaymentService.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly PaymentDbContext _dbContext;
    private readonly IBookingServiceClient _bookingClient;
    private readonly IStripeCheckoutService _stripeService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        PaymentDbContext dbContext,
        IBookingServiceClient bookingClient,
        IStripeCheckoutService stripeService,
        ILogger<PaymentsController> logger)
    {
        _dbContext = dbContext;
        _bookingClient = bookingClient;
        _stripeService = stripeService;
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
}
