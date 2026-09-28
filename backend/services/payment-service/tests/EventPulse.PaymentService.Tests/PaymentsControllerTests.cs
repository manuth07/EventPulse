using System.Security.Claims;
using System.Text;
using EventPulse.Contracts.Kafka;
using EventPulse.PaymentService.Controllers;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.DTOs;
using EventPulse.PaymentService.Events;
using EventPulse.PaymentService.Models;
using EventPulse.PaymentService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace EventPulse.PaymentService.Tests;

public class PaymentsControllerTests
{
    private readonly DbContextOptions<PaymentDbContext> _dbOptions;
    private readonly Mock<IBookingServiceClient> _bookingClientMock;
    private readonly Mock<IStripeCheckoutService> _stripeServiceMock;
    private readonly Mock<ILogger<PaymentsController>> _loggerMock;
    private readonly IConfiguration _configuration;
    private const string WebhookSecret = "whsec_test_secret_key_1234567890";

    public PaymentsControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _bookingClientMock = new Mock<IBookingServiceClient>();
        _stripeServiceMock = new Mock<IStripeCheckoutService>();
        _loggerMock = new Mock<ILogger<PaymentsController>>();

        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Stripe:WebhookSecret", WebhookSecret}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    private PaymentsController CreateController(PaymentDbContext dbContext, Guid? customerId = null, string? token = null, string? requestBody = null, string? signatureHeader = null)
    {
        var kafkaOptions = Microsoft.Extensions.Options.Options.Create(new KafkaOptions());
        var writerLogger = new Mock<ILogger<OutboxWriter>>();
        var outboxWriter = new OutboxWriter(dbContext, kafkaOptions, writerLogger.Object);

        var controller = new PaymentsController(
            dbContext,
            _bookingClientMock.Object,
            _stripeServiceMock.Object,
            outboxWriter,
            _configuration,
            _loggerMock.Object
        );

        var claims = new List<Claim>();
        if (customerId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, customerId.Value.ToString()));
            claims.Add(new Claim("sub", customerId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        if (!string.IsNullOrEmpty(token))
        {
            httpContext.Request.Headers["Authorization"] = $"Bearer {token}";
        }

        if (signatureHeader != null)
        {
            httpContext.Request.Headers["Stripe-Signature"] = signatureHeader;
        }

        if (requestBody != null)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
            httpContext.Request.Body = stream;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static string GenerateStripeSignature(string json, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes($"{timestamp}.{json}");
        using var hmac = new System.Security.Cryptography.HMACSHA256(secretBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return $"t={timestamp},v1={hashHex}";
    }

    [Fact]
    public async Task CreateCheckoutSession_WithValidBooking_ReturnsOkAndPersistsPayment()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var bookingRef = "EP-2026-TEST";
        var amount = 125.50m;
        var token = "valid_test_jwt";

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingSummaryDto(bookingId, bookingRef, customerId, amount, "PendingPayment"));

        _stripeServiceMock
            .Setup(s => s.CreateSessionAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSessionResult("cs_test_session_123", "https://checkout.stripe.com/pay/cs_test_session_123"));

        var controller = CreateController(db, customerId, token);
        var request = new CreateCheckoutSessionRequest { BookingId = bookingId };

        // Act
        var result = await controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CheckoutSessionResponse>(okResult.Value);
        Assert.Equal("cs_test_session_123", response.SessionId);
        Assert.Equal("https://checkout.stripe.com/pay/cs_test_session_123", response.CheckoutUrl);

        var savedPayment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        Assert.NotNull(savedPayment);
        Assert.Equal(bookingRef, savedPayment.BookingReference);
        Assert.Equal(customerId, savedPayment.CustomerId);
        Assert.Equal(amount, savedPayment.Amount);
        Assert.Equal(PaymentStatus.Pending, savedPayment.Status);
        Assert.Equal("cs_test_session_123", savedPayment.StripeSessionId);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenCustomerIdMismatches_ReturnsForbidden()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var authenticatedUser = Guid.NewGuid();
        var bookingOwner = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "jwt_user_a";

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingSummaryDto(bookingId, "EP-2026-FAIL", bookingOwner, 100.00m, "PendingPayment"));

        var controller = CreateController(db, authenticatedUser, token);
        var request = new CreateCheckoutSessionRequest { BookingId = bookingId };

        // Act
        var result = await controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objResult.StatusCode);

        var paymentsInDb = await db.Payments.ToListAsync();
        Assert.Empty(paymentsInDb);
    }

    [Fact]
    public async Task StripeWebhook_WithInvalidSignature_ReturnsBadRequest()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var body = "{\"id\":\"evt_test\"}";
        var controller = CreateController(db, requestBody: body, signatureHeader: "invalid_sig");

        // Act
        var result = await controller.StripeWebhook(CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task StripeWebhook_WithValidCheckoutSessionCompleted_UpdatesPaymentToSucceededAndPublishesEvent()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var sessionId = "cs_test_session_success_123";

        var payment = new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            BookingReference = "EP-2026-SUCCESS",
            CustomerId = Guid.NewGuid(),
            Amount = 1500.00m,
            Currency = "lkr",
            StripeSessionId = sessionId,
            Status = PaymentStatus.Pending
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var jsonPayload = $$"""
        {
          "id": "evt_test_123",
          "object": "event",
          "api_version": "2023-10-16",
          "created": 1700000000,
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "payment_intent": "pi_test_intent_success",
              "metadata": {
                "PaymentId": "{{paymentId}}",
                "BookingId": "{{bookingId}}"
              }
            }
          },
          "livemode": false,
          "pending_webhooks": 1,
          "request": {
            "id": "req_123",
            "idempotency_key": null
          },
          "type": "checkout.session.completed"
        }
        """;

        var signature = GenerateStripeSignature(jsonPayload, WebhookSecret);
        var controller = CreateController(db, requestBody: jsonPayload, signatureHeader: signature);

        // Act
        var result = await controller.StripeWebhook(CancellationToken.None);

        // Assert
        Assert.IsType<OkResult>(result);

        var updatedPayment = await db.Payments.FindAsync(paymentId);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatus.Succeeded, updatedPayment.Status);
        Assert.Equal("pi_test_intent_success", updatedPayment.StripePaymentIntentId);
        Assert.NotNull(updatedPayment.CompletedAt);

        var outboxMessage = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Topic == "payment-succeeded");
        Assert.NotNull(outboxMessage);
        Assert.Equal("PaymentSucceededEvent", outboxMessage.EventType);
        Assert.Equal(bookingId.ToString(), outboxMessage.MessageKey);
        Assert.NotEqual(Guid.Empty, outboxMessage.EventId);
        Assert.Null(outboxMessage.PublishedAtUtc);
        Assert.Equal(0, outboxMessage.PublishAttempts);

        var payload = System.Text.Json.JsonSerializer.Deserialize<PaymentSucceededEvent>(outboxMessage.Payload);
        Assert.NotNull(payload);
        Assert.Equal(outboxMessage.EventId, payload.EventId);
        Assert.Equal(paymentId, payload.PaymentId);
        Assert.Equal(bookingId, payload.BookingId);
        Assert.Equal("EP-2026-SUCCESS", payload.BookingReference);
    }

    [Fact]
    public async Task StripeWebhook_WhenAlreadySucceeded_IsIdempotentAndDoesNotPublishDuplicateEvent()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var sessionId = "cs_test_session_idempotent_123";

        var payment = new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            BookingReference = "EP-2026-IDEM",
            CustomerId = Guid.NewGuid(),
            Amount = 2000.00m,
            Currency = "lkr",
            StripeSessionId = sessionId,
            Status = PaymentStatus.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var jsonPayload = $$"""
        {
          "id": "evt_test_duplicate",
          "object": "event",
          "api_version": "2023-10-16",
          "created": 1700000000,
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "payment_intent": "pi_test_intent_123",
              "metadata": {
                "PaymentId": "{{paymentId}}"
              }
            }
          },
          "livemode": false,
          "pending_webhooks": 1,
          "request": {
            "id": "req_123",
            "idempotency_key": null
          },
          "type": "checkout.session.completed"
        }
        """;

        var signature = GenerateStripeSignature(jsonPayload, WebhookSecret);
        var controller = CreateController(db, requestBody: jsonPayload, signatureHeader: signature);

        // Act
        var result = await controller.StripeWebhook(CancellationToken.None);

        // Assert
        Assert.IsType<OkResult>(result);

        var outboxMessages = await db.OutboxMessages.ToListAsync();
        Assert.Empty(outboxMessages);
    }

    [Fact]
    public async Task StripeWebhook_WithValidPaymentIntentFailed_UpdatesPaymentToFailedAndPublishesEvent()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentIntentId = "pi_test_intent_fail_999";

        var payment = new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            BookingReference = "EP-2026-FAIL",
            CustomerId = Guid.NewGuid(),
            Amount = 1000.00m,
            Currency = "lkr",
            StripePaymentIntentId = paymentIntentId,
            Status = PaymentStatus.Pending
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var jsonPayload = $$"""
        {
          "id": "evt_test_failed_123",
          "object": "event",
          "api_version": "2023-10-16",
          "created": 1700000000,
          "data": {
            "object": {
              "id": "{{paymentIntentId}}",
              "object": "payment_intent",
              "last_payment_error": {
                "message": "Insufficient funds"
              },
              "metadata": {
                "PaymentId": "{{paymentId}}"
              }
            }
          },
          "livemode": false,
          "pending_webhooks": 1,
          "request": {
            "id": "req_123",
            "idempotency_key": null
          },
          "type": "payment_intent.payment_failed"
        }
        """;

        var signature = GenerateStripeSignature(jsonPayload, WebhookSecret);
        var controller = CreateController(db, requestBody: jsonPayload, signatureHeader: signature);

        // Act
        var result = await controller.StripeWebhook(CancellationToken.None);

        // Assert
        Assert.IsType<OkResult>(result);

        var updatedPayment = await db.Payments.FindAsync(paymentId);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatus.Failed, updatedPayment.Status);

        var outboxMessage = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Topic == "payment-failed");
        Assert.NotNull(outboxMessage);
        Assert.Equal("PaymentFailedEvent", outboxMessage.EventType);
        Assert.Equal(bookingId.ToString(), outboxMessage.MessageKey);
        Assert.NotEqual(Guid.Empty, outboxMessage.EventId);
        Assert.Null(outboxMessage.PublishedAtUtc);

        var payload = System.Text.Json.JsonSerializer.Deserialize<PaymentFailedEvent>(outboxMessage.Payload);
        Assert.NotNull(payload);
        Assert.Equal(outboxMessage.EventId, payload.EventId);
        Assert.Equal(paymentId, payload.PaymentId);
        Assert.Equal("Insufficient funds", payload.FailureReason);
    }
}
