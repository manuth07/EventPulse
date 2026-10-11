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

    private PaymentsController CreateController(PaymentDbContext dbContext, Guid? customerId = null, string? token = null, string? requestBody = null, string? signatureHeader = null, string? role = null)
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

        if (!string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
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

    [Fact]
    public async Task CreateCheckoutSession_WhenBookingStatusIsPaymentFailed_AllowsPaymentAndCreatesSession()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "test_bearer_token";

        var bookingSummary = new BookingSummaryDto
        {
            Id = bookingId,
            BookingReference = "EP-FAIL-RETRY",
            CustomerId = customerId,
            TotalAmount = 75.0m,
            Status = "PaymentFailed"
        };

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookingSummary);

        _stripeServiceMock
            .Setup(s => s.CreateSessionAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSessionResult("cs_retry_123", "https://checkout.stripe.com/pay/cs_retry_123"));

        var controller = CreateController(db, customerId: customerId, token: token);

        // Act
        var result = await controller.CreateCheckoutSession(
            new CreateCheckoutSessionRequest { BookingId = bookingId },
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CheckoutSessionResponse>(okResult.Value);
        Assert.Equal(bookingId, response.BookingId);
        Assert.Equal("cs_retry_123", response.SessionId);
    }

    [Theory]
    [InlineData("Confirmed")]
    [InlineData("Cancelled")]
    public async Task CreateCheckoutSession_WhenBookingStatusIsInvalid_ReturnsBadRequest(string invalidStatus)
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "test_bearer_token";

        var bookingSummary = new BookingSummaryDto
        {
            Id = bookingId,
            BookingReference = "EP-INVALID-STATUS",
            CustomerId = customerId,
            TotalAmount = 75.0m,
            Status = invalidStatus
        };

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookingSummary);

        var controller = CreateController(db, customerId: customerId, token: token);

        // Act
        var result = await controller.CreateCheckoutSession(
            new CreateCheckoutSessionRequest { BookingId = bookingId },
            CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateCheckoutSession_WhenBookingReferenceIsMissing_ReturnsBadRequest(string? missingRef)
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "test_bearer_token";

        var bookingSummary = new BookingSummaryDto
        {
            Id = bookingId,
            BookingReference = missingRef!,
            CustomerId = customerId,
            TotalAmount = 100.0m,
            Status = "PendingPayment"
        };

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookingSummary);

        var controller = CreateController(db, customerId: customerId, token: token);

        // Act
        var result = await controller.CreateCheckoutSession(
            new CreateCheckoutSessionRequest { BookingId = bookingId },
            CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
        var paymentsInDb = await db.Payments.ToListAsync();
        Assert.Empty(paymentsInDb);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenBookingIdIsEmpty_ReturnsBadRequest()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "test_bearer_token";

        var bookingSummary = new BookingSummaryDto
        {
            Id = Guid.Empty,
            BookingReference = "EP-2026-EMPTY",
            CustomerId = customerId,
            TotalAmount = 100.0m,
            Status = "PendingPayment"
        };

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookingSummary);

        var controller = CreateController(db, customerId: customerId, token: token);

        // Act
        var result = await controller.CreateCheckoutSession(
            new CreateCheckoutSessionRequest { BookingId = bookingId },
            CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenBookingAlreadyHasSucceededPayment_ReturnsBadRequest()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "test_bearer_token";

        // Pre-existing Succeeded payment in DB
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = "EP-2026-PAID",
            CustomerId = customerId,
            Amount = 1500m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var bookingSummary = new BookingSummaryDto
        {
            Id = bookingId,
            BookingReference = "EP-2026-PAID",
            CustomerId = customerId,
            TotalAmount = 1500m,
            Status = "PendingPayment"
        };

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookingSummary);

        var controller = CreateController(db, customerId: customerId, token: token);

        // Act
        var result = await controller.CreateCheckoutSession(
            new CreateCheckoutSessionRequest { BookingId = bookingId },
            CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);

        // Ensure no new duplicate payment was created
        var totalPayments = await db.Payments.CountAsync(p => p.BookingId == bookingId);
        Assert.Equal(1, totalPayments);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenRetryingFailedPayment_CreatesNewAttemptAssociatedWithSameBooking()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var bookingRef = "EP-2026-RETRY-OK";
        var token = "valid_jwt";

        // Previous attempt that failed
        var failedPayment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = bookingRef,
            CustomerId = customerId,
            Amount = 2500m,
            Currency = "lkr",
            Status = PaymentStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-29)
        };
        db.Payments.Add(failedPayment);
        await db.SaveChangesAsync();

        var bookingSummary = new BookingSummaryDto(bookingId, bookingRef, customerId, 2500m, "PaymentFailed");

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookingSummary);

        _stripeServiceMock
            .Setup(s => s.CreateSessionAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSessionResult("cs_retry_success_456", "https://checkout.stripe.com/pay/cs_retry_success_456"));

        var controller = CreateController(db, customerId, token);

        // Act
        var result = await controller.CreateCheckoutSession(new CreateCheckoutSessionRequest { BookingId = bookingId }, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CheckoutSessionResponse>(okResult.Value);
        Assert.NotEqual(failedPayment.Id, response.PaymentId);
        Assert.Equal(bookingId, response.BookingId);
        Assert.Equal(bookingRef, response.BookingReference);

        var payments = await db.Payments.Where(p => p.BookingId == bookingId).OrderBy(p => p.CreatedAt).ToListAsync();
        Assert.Equal(2, payments.Count);

        // Verify First attempt
        Assert.Equal(failedPayment.Id, payments[0].Id);
        Assert.Equal(PaymentStatus.Failed, payments[0].Status);
        Assert.Equal(bookingId, payments[0].BookingId);
        Assert.Equal(bookingRef, payments[0].BookingReference);
        Assert.Equal(customerId, payments[0].CustomerId);

        // Verify Second attempt (retry)
        Assert.Equal(response.PaymentId, payments[1].Id);
        Assert.Equal(PaymentStatus.Pending, payments[1].Status);
        Assert.Equal(bookingId, payments[1].BookingId);
        Assert.Equal(bookingRef, payments[1].BookingReference);
        Assert.Equal(customerId, payments[1].CustomerId);
        Assert.Equal(2500m, payments[1].Amount);
        Assert.Equal("cs_retry_success_456", payments[1].StripeSessionId);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenRetryingWhilePending_ReusesExistingPendingPaymentWithoutDuplicates()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var bookingRef = "EP-2026-REUSE-PENDING";
        var token = "valid_jwt";

        // Existing pending payment
        var existingPending = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = bookingRef,
            CustomerId = customerId,
            Amount = 1000m,
            Currency = "lkr",
            Status = PaymentStatus.Pending,
            StripeSessionId = "cs_old_session",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        db.Payments.Add(existingPending);
        await db.SaveChangesAsync();

        var bookingSummary = new BookingSummaryDto(bookingId, bookingRef, customerId, 1000m, "PendingPayment");

        _bookingClientMock
            .Setup(c => c.GetBookingSummaryAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookingSummary);

        _stripeServiceMock
            .Setup(s => s.CreateSessionAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSessionResult("cs_new_session", "https://checkout.stripe.com/pay/cs_new_session"));

        var controller = CreateController(db, customerId, token);

        // Act
        var result = await controller.CreateCheckoutSession(new CreateCheckoutSessionRequest { BookingId = bookingId }, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CheckoutSessionResponse>(okResult.Value);
        Assert.Equal(existingPending.Id, response.PaymentId);

        var payments = await db.Payments.Where(p => p.BookingId == bookingId).ToListAsync();
        Assert.Single(payments);
        Assert.Equal(existingPending.Id, payments[0].Id);
        Assert.Equal("cs_new_session", payments[0].StripeSessionId);
        Assert.Equal(PaymentStatus.Pending, payments[0].Status);
    }

    [Fact]
    public async Task GetPaymentsByBooking_WhenPaymentsExist_ReturnsAllPaymentsForBooking()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var bookingRef = "EP-2026-HISTORY-1";

        var p1 = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = bookingRef,
            CustomerId = customerId,
            Amount = 3000m,
            Currency = "lkr",
            Status = PaymentStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-1).AddMinutes(2)
        };

        var p2 = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = bookingRef,
            CustomerId = customerId,
            Amount = 3000m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded,
            StripeSessionId = "cs_history_succeeded",
            StripePaymentIntentId = "pi_history_succeeded",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-28)
        };

        db.Payments.AddRange(p1, p2);
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: customerId);

        // Act
        var result = await controller.GetPaymentsByBooking(bookingId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsType<List<PaymentDto>>(okResult.Value);
        Assert.Equal(2, list.Count);

        // Ordered by CreatedAt descending
        Assert.Equal(p2.Id, list[0].Id);
        Assert.Equal(bookingId, list[0].BookingId);
        Assert.Equal(bookingRef, list[0].BookingReference);
        Assert.Equal(customerId, list[0].CustomerId);
        Assert.Equal(3000m, list[0].Amount);
        Assert.Equal("lkr", list[0].Currency);
        Assert.Equal("Succeeded", list[0].Status);
        Assert.Equal("cs_history_succeeded", list[0].StripeSessionId);
        Assert.Equal("pi_history_succeeded", list[0].StripePaymentIntentId);
        Assert.NotNull(list[0].CompletedAt);

        Assert.Equal(p1.Id, list[1].Id);
        Assert.Equal("Failed", list[1].Status);
    }

    [Fact]
    public async Task GetPaymentsByBooking_WhenCustomerMismatches_ReturnsForbidden()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = "EP-2026-FORBIDDEN",
            CustomerId = ownerId,
            Amount = 1000m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: requesterId);

        // Act
        var result = await controller.GetPaymentsByBooking(bookingId, CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetPaymentsByBooking_WhenAdminRequests_ReturnsOkRegardlessOfCustomerId()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = "EP-2026-ADMIN",
            CustomerId = ownerId,
            Amount = 1000m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: adminId, role: "Administrator");

        // Act
        var result = await controller.GetPaymentsByBooking(bookingId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsType<List<PaymentDto>>(okResult.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetPaymentsByBooking_WhenNoPaymentsExist_ReturnsNotFound()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var controller = CreateController(db, customerId: Guid.NewGuid());

        // Act
        var result = await controller.GetPaymentsByBooking(Guid.NewGuid(), CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task GetPaymentById_WhenExists_ReturnsPaymentDetails()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        db.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            BookingReference = "EP-2026-SINGLE",
            CustomerId = customerId,
            Amount = 1800m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: customerId);

        // Act
        var result = await controller.GetPaymentById(paymentId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CustomerPaymentDetailDto>(okResult.Value);
        Assert.Equal(paymentId, dto.Id);
        Assert.Equal(bookingId, dto.BookingId);
        Assert.Equal("EP-2026-SINGLE", dto.BookingReference);
        Assert.Equal(customerId, dto.CustomerId);
        Assert.Equal(1800m, dto.Amount);
        Assert.Equal("Succeeded", dto.Status);
    }

    [Fact]
    public async Task StripeWebhook_CheckoutSessionCompleted_WhenNotFoundBySessionId_FallbackLinksByBookingId()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var bookingRef = "EP-2026-FALLBACK-OK";

        var payment = new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            BookingReference = bookingRef,
            CustomerId = Guid.NewGuid(),
            Amount = 1400m,
            Currency = "lkr",
            StripeSessionId = "cs_different_session_id",
            Status = PaymentStatus.Pending
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var jsonPayload = $$"""
        {
          "id": "evt_test_fallback",
          "object": "event",
          "api_version": "2023-10-16",
          "created": 1700000000,
          "data": {
            "object": {
              "id": "cs_webhook_session_unknown",
              "object": "checkout.session",
              "payment_intent": "pi_fallback_success",
              "metadata": {
                "BookingId": "{{bookingId}}",
                "BookingReference": "{{bookingRef}}"
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
        Assert.Equal("pi_fallback_success", updatedPayment.StripePaymentIntentId);
        Assert.NotNull(updatedPayment.CompletedAt);
    }

    [Fact]
    public async Task StripeWebhook_WhenPaymentBookingReferenceEmpty_RepairsBookingReferenceFromMetadata()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var sessionId = "cs_test_repair_ref";

        var payment = new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            BookingReference = "",
            CustomerId = Guid.NewGuid(),
            Amount = 1400m,
            Currency = "lkr",
            StripeSessionId = sessionId,
            Status = PaymentStatus.Pending
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var jsonPayload = $$"""
        {
          "id": "evt_test_repair",
          "object": "event",
          "api_version": "2023-10-16",
          "created": 1700000000,
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "payment_intent": "pi_repair_success",
              "metadata": {
                "PaymentId": "{{paymentId}}",
                "BookingId": "{{bookingId}}",
                "BookingReference": "EP-2026-REPAIRED"
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
        Assert.Equal("EP-2026-REPAIRED", updatedPayment.BookingReference);
    }

    [Fact]
    public async Task GetMyPayments_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var controller = CreateController(db, customerId: null); // No customerId claim

        // Act
        var result = await controller.GetMyPayments(new PaymentHistoryQueryParameters(), CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task GetMyPayments_WhenEmpty_ReturnsOkWithEmptyCollection()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var controller = CreateController(db, customerId: customerId);

        // Act
        var result = await controller.GetMyPayments(new PaymentHistoryQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var pagedResult = Assert.IsType<PagedResult<CustomerPaymentHistoryItemDto>>(okResult.Value);
        Assert.NotNull(pagedResult.Items);
        Assert.Empty(pagedResult.Items);
        Assert.Equal(0, pagedResult.TotalCount);
        Assert.Equal(1, pagedResult.Page);
        Assert.Equal(10, pagedResult.PageSize);
        Assert.False(pagedResult.HasNextPage);
        Assert.False(pagedResult.HasPreviousPage);
    }

    [Fact]
    public async Task GetMyPayments_ReturnsOnlyAuthenticatedCustomerPayments_OrderedByMostRecent()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();

        // Customer's older payment
        var p1 = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-MINE-1",
            CustomerId = customerId,
            Amount = 1000m,
            Currency = "lkr",
            Status = PaymentStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };

        // Customer's newer payment
        var p2 = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-MINE-2",
            CustomerId = customerId,
            Amount = 2500m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-9)
        };

        // Another customer's payment
        var pOther = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-OTHER",
            CustomerId = otherCustomerId,
            Amount = 9999m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        db.Payments.AddRange(p1, p2, pOther);
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: customerId);

        // Act
        var result = await controller.GetMyPayments(new PaymentHistoryQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var pagedResult = Assert.IsType<PagedResult<CustomerPaymentHistoryItemDto>>(okResult.Value);
        Assert.Equal(2, pagedResult.TotalCount);
        Assert.Equal(2, pagedResult.Items.Count);

        // Most recent first: p2 then p1
        Assert.Equal(p2.Id, pagedResult.Items[0].Id);
        Assert.Equal("EP-2026-MINE-2", pagedResult.Items[0].BookingReference);
        Assert.Equal(2500m, pagedResult.Items[0].Amount);
        Assert.Equal("Succeeded", pagedResult.Items[0].Status);

        Assert.Equal(p1.Id, pagedResult.Items[1].Id);
        Assert.Equal("EP-2026-MINE-1", pagedResult.Items[1].BookingReference);
        Assert.Equal(1000m, pagedResult.Items[1].Amount);
        Assert.Equal("Failed", pagedResult.Items[1].Status);

        // Ensure other customer's payment is strictly excluded
        Assert.DoesNotContain(pagedResult.Items, i => i.BookingReference == "EP-2026-OTHER");
    }

    [Fact]
    public async Task GetMyPayments_SupportsPagination_CorrectPageAndPageSize()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();

        for (int i = 1; i <= 5; i++)
        {
            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = Guid.NewGuid(),
                BookingReference = $"EP-2026-PAGE-{i}",
                CustomerId = customerId,
                Amount = i * 100m,
                Currency = "lkr",
                Status = PaymentStatus.Succeeded,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: customerId);

        // Act - Request Page 1 with pageSize 2
        var resultPage1 = await controller.GetMyPayments(new PaymentHistoryQueryParameters { Page = 1, PageSize = 2 }, CancellationToken.None);
        var okPage1 = Assert.IsType<OkObjectResult>(resultPage1);
        var page1 = Assert.IsType<PagedResult<CustomerPaymentHistoryItemDto>>(okPage1.Value);

        // Assert Page 1
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal(3, page1.TotalPages);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);

        // Act - Request Page 3 with pageSize 2
        var resultPage3 = await controller.GetMyPayments(new PaymentHistoryQueryParameters { Page = 3, PageSize = 2 }, CancellationToken.None);
        var okPage3 = Assert.IsType<OkObjectResult>(resultPage3);
        var page3 = Assert.IsType<PagedResult<CustomerPaymentHistoryItemDto>>(okPage3.Value);

        // Assert Page 3
        Assert.Single(page3.Items);
        Assert.Equal(3, page3.Page);
        Assert.False(page3.HasNextPage);
        Assert.True(page3.HasPreviousPage);
    }

    [Fact]
    public async Task GetMyPayments_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-SUCC",
            CustomerId = customerId,
            Amount = 100m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-FAIL",
            CustomerId = customerId,
            Amount = 100m,
            Currency = "lkr",
            Status = PaymentStatus.Failed
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: customerId);

        // Act
        var result = await controller.GetMyPayments(new PaymentHistoryQueryParameters { Status = "Succeeded" }, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var pagedResult = Assert.IsType<PagedResult<CustomerPaymentHistoryItemDto>>(okResult.Value);
        Assert.Single(pagedResult.Items);
        Assert.Equal("EP-2026-SUCC", pagedResult.Items[0].BookingReference);
        Assert.Equal("Succeeded", pagedResult.Items[0].Status);
    }

    [Fact]
    public async Task GetMyPayments_WithMultipleAttemptsForOneBooking_ReturnsAllAttempts()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var bookingRef = "EP-2026-MULTI-ATTEMPT";

        // Attempt 1: Failed
        var attempt1 = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = bookingRef,
            CustomerId = customerId,
            Amount = 4500m,
            Currency = "lkr",
            Status = PaymentStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        };

        // Attempt 2: Succeeded (retry)
        var attempt2 = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = bookingRef,
            CustomerId = customerId,
            Amount = 4500m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-4)
        };

        db.Payments.AddRange(attempt1, attempt2);
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: customerId);

        // Act
        var result = await controller.GetMyPayments(new PaymentHistoryQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var pagedResult = Assert.IsType<PagedResult<CustomerPaymentHistoryItemDto>>(okResult.Value);
        Assert.Equal(2, pagedResult.Items.Count);

        // Both attempts belong to the same booking
        Assert.All(pagedResult.Items, i => Assert.Equal(bookingId, i.BookingId));
        Assert.All(pagedResult.Items, i => Assert.Equal(bookingRef, i.BookingReference));

        // Ordered by most recent: attempt2 first
        Assert.Equal(attempt2.Id, pagedResult.Items[0].Id);
        Assert.Equal("Succeeded", pagedResult.Items[0].Status);

        Assert.Equal(attempt1.Id, pagedResult.Items[1].Id);
        Assert.Equal("Failed", pagedResult.Items[1].Status);
    }

    [Fact]
    public async Task GetMyPayments_EnrichesWithEventName_WhenAvailableFromBookingService()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var token = "valid_jwt_token";

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = "EP-2026-EVENT-ENRICH",
            CustomerId = customerId,
            Amount = 3500m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        _bookingClientMock
            .Setup(c => c.GetBookingEventNameAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Tech Music Festival 2026");

        var controller = CreateController(db, customerId: customerId, token: token);

        // Act
        var result = await controller.GetMyPayments(new PaymentHistoryQueryParameters(), CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var pagedResult = Assert.IsType<PagedResult<CustomerPaymentHistoryItemDto>>(okResult.Value);
        Assert.Single(pagedResult.Items);
        Assert.Equal("Tech Music Festival 2026", pagedResult.Items[0].EventName);
    }

    [Fact]
    public async Task GetPaymentById_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var controller = CreateController(db, customerId: null);

        // Act
        var result = await controller.GetPaymentById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task GetPaymentById_WhenInvalidGuid_ReturnsBadRequest()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var controller = CreateController(db, customerId: Guid.NewGuid());

        // Act
        var result = await controller.GetPaymentById(Guid.Empty, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetPaymentById_WhenPaymentBelongsToOtherCustomer_ReturnsNotFoundToHideExistence()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        db.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-SECRET",
            CustomerId = ownerId,
            Amount = 5000m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: attackerId);

        // Act
        var result = await controller.GetPaymentById(paymentId, CancellationToken.None);

        // Assert - MUST be 404 to hide the existence of the resource
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task GetPaymentById_WhenAdminRequests_ReturnsOkRegardlessOfOwner()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        db.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = Guid.NewGuid(),
            BookingReference = "EP-2026-ADMIN-VIEW",
            CustomerId = ownerId,
            Amount = 4000m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, customerId: adminId, role: "Administrator");

        // Act
        var result = await controller.GetPaymentById(paymentId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<CustomerPaymentDetailDto>(okResult.Value);
        Assert.Equal(paymentId, detail.Id);
        Assert.Equal(ownerId, detail.CustomerId);
    }

    [Fact]
    public async Task GetPaymentById_EnrichesWithEventName_WhenAvailableFromBookingService()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var token = "valid_jwt_token";

        db.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            BookingReference = "EP-2026-DETAIL-ENRICH",
            CustomerId = customerId,
            Amount = 3500m,
            Currency = "lkr",
            Status = PaymentStatus.Succeeded,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        _bookingClientMock
            .Setup(c => c.GetBookingEventNameAsync(bookingId, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Rock Night 2026");

        var controller = CreateController(db, customerId: customerId, token: token);

        // Act
        var result = await controller.GetPaymentById(paymentId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<CustomerPaymentDetailDto>(okResult.Value);
        Assert.Equal("Rock Night 2026", detail.EventName);
        Assert.Equal(bookingId, detail.BookingId);
        Assert.Equal("EP-2026-DETAIL-ENRICH", detail.BookingReference);
    }
}
