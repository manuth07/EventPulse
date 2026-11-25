using EventPulse.Contracts.Kafka;
using EventPulse.Contracts.Kafka.Events;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.Events;
using EventPulse.PaymentService.Models;
using EventPulse.PaymentService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.PaymentService.Tests;

/// <summary>
/// SE3112 - Unit Testing &amp; Mocking (second component: payment refunds).
/// Parameterized (Theory) tests for PaymentRefundService business rules:
///   1) refund amount is capped to what is still refundable,
///   2) payments in a non-refundable status are rejected without calling Stripe,
///   3) zero or negative refund amounts are rejected without calling Stripe,
///   4) the Stripe outcome (succeeded / pending / failed) decides the refund and payment status.
/// IStripeRefundService and IPaymentEventPublisher are Moq mocks, so no real Stripe call or Kafka message is made.
/// </summary>
[Trait("Category", "SE3112-Unit")]
public class PaymentRefundParameterizedTests
{
    private const decimal PaymentAmount = 150m;

    private readonly DbContextOptions<PaymentDbContext> _dbOptions;
    private readonly Mock<ILogger<PaymentRefundService>> _loggerMock;
    private readonly Mock<IPaymentEventPublisher> _publisherMock;

    public PaymentRefundParameterizedTests()
    {
        // A uniquely named in-memory database per test instance keeps every test independent.
        _dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _loggerMock = new Mock<ILogger<PaymentRefundService>>();
        _publisherMock = new Mock<IPaymentEventPublisher>();
    }

    private PaymentRefundService CreateService(PaymentDbContext dbContext, IStripeRefundService stripeRefundService)
    {
        var kafkaOptions = Options.Create(new KafkaOptions());
        var writerLogger = new Mock<ILogger<OutboxWriter>>();
        var outboxWriter = new OutboxWriter(dbContext, kafkaOptions, writerLogger.Object);

        return new PaymentRefundService(
            dbContext,
            outboxWriter,
            _loggerMock.Object,
            _publisherMock.Object,
            stripeRefundService);
    }

    private static Payment NewPayment(PaymentStatus status) => new()
    {
        Id = Guid.NewGuid(),
        BookingId = Guid.NewGuid(),
        BookingReference = "EP-SE3112-REF",
        CustomerId = Guid.NewGuid(),
        StripePaymentIntentId = "pi_test_se3112",
        Amount = PaymentAmount,
        Currency = "usd",
        Status = status,
        CompletedAt = DateTimeOffset.UtcNow
    };

    private static BookingRefundRequestedEvent NewRequest(Payment payment, decimal amount, Guid requestId) => new(
        RefundRequestId: requestId,
        BookingId: payment.BookingId,
        BookingReference: payment.BookingReference,
        CustomerId: payment.CustomerId,
        EventId: Guid.NewGuid(),
        RefundAmount: amount,
        Reason: "Event cancelled",
        RequestedAt: DateTimeOffset.UtcNow);

    /// <summary>Stub: Stripe always answers with the given result.</summary>
    private static Mock<IStripeRefundService> StripeReturns(StripeRefundResult result)
    {
        var mock = new Mock<IStripeRefundService>();
        mock.Setup(s => s.CreateRefundAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    private static void VerifyStripeNeverCalled(Mock<IStripeRefundService> stripeMock) =>
        stripeMock.Verify(s => s.CreateRefundAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());

    // ---------------------------------------------------------------------
    // 1) Refund amount is capped to the remaining refundable amount (payment = 150)
    // ---------------------------------------------------------------------
    [Theory]
    //          alreadyRefunded requested expectedToStripe expectedPaymentStatus
    [InlineData(0,   50,  50,  PaymentStatus.PartiallyRefunded)] // partial refund
    [InlineData(0,   500, 150, PaymentStatus.Refunded)]          // request above payment: capped to 150
    [InlineData(100, 80,  50,  PaymentStatus.Refunded)]          // only 50 left: capped to remaining
    public async Task ProcessRefund_CapsAmountToRemainingEligible_AndSetsPaymentStatus(
        int alreadyRefunded, int requested, int expectedToStripe, PaymentStatus expectedPaymentStatus)
    {
        // Arrange
        using var dbContext = new PaymentDbContext(_dbOptions);
        var payment = NewPayment(alreadyRefunded > 0 ? PaymentStatus.PartiallyRefunded : PaymentStatus.Succeeded);
        if (alreadyRefunded > 0)
        {
            payment.RefundRecords.Add(new RefundRecord
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                BookingId = payment.BookingId,
                Amount = alreadyRefunded,
                Currency = "usd",
                Status = RefundStatus.Completed,
                Reason = "Earlier partial refund",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var requestId = Guid.NewGuid();
        var stripeMock = StripeReturns(new StripeRefundResult(true, "re_test_123", "succeeded", null));
        var service = CreateService(dbContext, stripeMock.Object);

        // Act
        var record = await service.ProcessRefundAsync(NewRequest(payment, requested, requestId));

        // Assert: outcome
        Assert.NotNull(record);
        Assert.Equal(RefundStatus.Completed, record.Status);
        Assert.Equal((decimal)expectedToStripe, record.Amount);

        var storedPayment = await dbContext.Payments.FirstAsync(p => p.Id == payment.Id);
        Assert.Equal(expectedPaymentStatus, storedPayment.Status);

        // Assert: Stripe was called once with the capped amount and the idempotency key
        stripeMock.Verify(s => s.CreateRefundAsync(
                "pi_test_se3112", (decimal)expectedToStripe, "usd",
                It.IsAny<string>(), $"refund-{requestId}", It.IsAny<CancellationToken>()),
            Times.Once());
    }

    // ---------------------------------------------------------------------
    // 2) Non-refundable payment status: NotRefundable record, Stripe never called
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.RefundFailed)]
    public async Task ProcessRefund_WhenPaymentNotRefundable_ReturnsNotRefundableAndSkipsStripe(PaymentStatus status)
    {
        // Arrange
        using var dbContext = new PaymentDbContext(_dbOptions);
        var payment = NewPayment(status);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var stripeMock = new Mock<IStripeRefundService>();
        var service = CreateService(dbContext, stripeMock.Object);

        // Act
        var record = await service.ProcessRefundAsync(NewRequest(payment, 50m, Guid.NewGuid()));

        // Assert
        Assert.NotNull(record);
        Assert.Equal(RefundStatus.NotRefundable, record.Status);
        Assert.Equal(0m, record.Amount);

        VerifyStripeNeverCalled(stripeMock);
        _publisherMock.Verify(p => p.PublishPaymentRefundedAsync(
            It.IsAny<PaymentRefundedEvent>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // ---------------------------------------------------------------------
    // 3) Boundary: zero and negative refund amounts
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task ProcessRefund_WhenAmountIsZeroOrNegative_ReturnsNotRefundableAndSkipsStripe(int requested)
    {
        // Arrange
        using var dbContext = new PaymentDbContext(_dbOptions);
        var payment = NewPayment(PaymentStatus.Succeeded);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var stripeMock = new Mock<IStripeRefundService>();
        var service = CreateService(dbContext, stripeMock.Object);

        // Act
        var record = await service.ProcessRefundAsync(NewRequest(payment, requested, Guid.NewGuid()));

        // Assert
        Assert.NotNull(record);
        Assert.Equal(RefundStatus.NotRefundable, record.Status);
        Assert.Equal(0m, record.Amount);

        var storedPayment = await dbContext.Payments.FirstAsync(p => p.Id == payment.Id);
        Assert.Equal(PaymentStatus.Succeeded, storedPayment.Status); // payment left untouched

        VerifyStripeNeverCalled(stripeMock);
    }

    // ---------------------------------------------------------------------
    // 4) Stripe outcome decides the refund and payment status
    // ---------------------------------------------------------------------
    [Theory]
    //          success stripeStatus  failureReason                 expectedRefund          expectedPayment
    [InlineData(true,  "succeeded", null,                          RefundStatus.Completed, PaymentStatus.Refunded)]
    [InlineData(true,  "pending",   null,                          RefundStatus.Pending,   PaymentStatus.Succeeded)]    // wait for webhook
    [InlineData(false, null,        "Card network unavailable",    RefundStatus.Failed,    PaymentStatus.RefundFailed)]
    public async Task ProcessRefund_MapsStripeOutcome_ToRefundAndPaymentStatus(
        bool success, string? stripeStatus, string? failureReason,
        RefundStatus expectedRefundStatus, PaymentStatus expectedPaymentStatus)
    {
        // Arrange
        using var dbContext = new PaymentDbContext(_dbOptions);
        var payment = NewPayment(PaymentStatus.Succeeded);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var stripeResult = new StripeRefundResult(success, success ? "re_test_123" : null, stripeStatus, failureReason);
        var stripeMock = StripeReturns(stripeResult);
        var service = CreateService(dbContext, stripeMock.Object);

        // Act (full refund of the payment)
        var record = await service.ProcessRefundAsync(NewRequest(payment, PaymentAmount, Guid.NewGuid()));

        // Assert: outcome
        Assert.NotNull(record);
        Assert.Equal(expectedRefundStatus, record.Status);
        if (!success)
        {
            Assert.Equal(failureReason, record.FailureReason);
        }

        var storedPayment = await dbContext.Payments.FirstAsync(p => p.Id == payment.Id);
        Assert.Equal(expectedPaymentStatus, storedPayment.Status);

        // Assert: the refunded event is only published when the refund really completed
        _publisherMock.Verify(p => p.PublishPaymentRefundedAsync(
                It.IsAny<PaymentRefundedEvent>(), It.IsAny<CancellationToken>()),
            expectedRefundStatus == RefundStatus.Completed ? Times.Once() : Times.Never());
    }
}