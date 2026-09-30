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

public class PaymentRefundServiceTests
{
    private readonly DbContextOptions<PaymentDbContext> _dbOptions;
    private readonly Mock<ILogger<PaymentRefundService>> _loggerMock;
    private readonly Mock<IPaymentEventPublisher> _publisherMock;

    public PaymentRefundServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _loggerMock = new Mock<ILogger<PaymentRefundService>>();
        _publisherMock = new Mock<IPaymentEventPublisher>();
    }

    private (PaymentRefundService Service, PaymentDbContext DbContext, OutboxWriter OutboxWriter) CreateService(PaymentDbContext dbContext)
    {
        var kafkaOptions = Options.Create(new KafkaOptions());
        var writerLogger = new Mock<ILogger<OutboxWriter>>();
        var outboxWriter = new OutboxWriter(dbContext, kafkaOptions, writerLogger.Object);

        var service = new PaymentRefundService(
            dbContext,
            outboxWriter,
            _loggerMock.Object,
            _publisherMock.Object
        );

        return (service, dbContext, outboxWriter);
    }

    [Fact]
    public async Task ProcessRefundAsync_ValidPayment_CreatesRefundRecordAndUpdatesStatus()
    {
        // Arrange
        using var dbContext = new PaymentDbContext(_dbOptions);
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = "EP-REF-001",
            CustomerId = customerId,
            Amount = 150.00m,
            Currency = "usd",
            Status = PaymentStatus.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var (service, _, _) = CreateService(dbContext);

        var refundRequest = new BookingRefundRequestedEvent(
            RefundRequestId: Guid.NewGuid(),
            BookingId: bookingId,
            BookingReference: "EP-REF-001",
            CustomerId: customerId,
            EventId: Guid.NewGuid(),
            RefundAmount: 150.00m,
            Reason: "Event cancelled",
            RequestedAt: DateTimeOffset.UtcNow
        );

        // Act
        var refundRecord = await service.ProcessRefundAsync(refundRequest);

        // Assert
        Assert.NotNull(refundRecord);
        Assert.Equal(refundRequest.RefundRequestId, refundRecord.Id);
        Assert.Equal(150.00m, refundRecord.Amount);
        Assert.Equal("Event cancelled", refundRecord.Reason);

        var updatedPayment = await dbContext.Payments
            .Include(p => p.RefundRecords)
            .FirstOrDefaultAsync(p => p.Id == payment.Id);

        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatus.Refunded, updatedPayment.Status);
        Assert.Single(updatedPayment.RefundRecords);
        Assert.Equal(150.00m, updatedPayment.RefundRecords[0].Amount);

        // Verify outbox record created
        var outboxMessages = await dbContext.OutboxMessages.ToListAsync();
        Assert.Contains(outboxMessages, m => m.EventType == nameof(PaymentRefundedEvent));

        // Verify event publisher called
        _publisherMock.Verify(p => p.PublishPaymentRefundedAsync(
            It.Is<PaymentRefundedEvent>(e => e.PaymentId == payment.Id && e.Amount == 150.00m && e.Status == PaymentStatus.Refunded.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundAsync_AlreadyRefunded_HandlesIdempotently()
    {
        // Arrange
        using var dbContext = new PaymentDbContext(_dbOptions);
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var existingRefundId = Guid.NewGuid();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = "EP-REF-002",
            CustomerId = customerId,
            Amount = 100.00m,
            Currency = "usd",
            Status = PaymentStatus.Refunded,
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var existingRefund = new RefundRecord
        {
            Id = existingRefundId,
            PaymentId = payment.Id,
            BookingId = bookingId,
            Amount = 100.00m,
            Reason = "Already refunded previously",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            Payment = payment
        };

        dbContext.Payments.Add(payment);
        dbContext.RefundRecords.Add(existingRefund);
        await dbContext.SaveChangesAsync();

        var (service, _, _) = CreateService(dbContext);

        var duplicateRefundRequest = new BookingRefundRequestedEvent(
            RefundRequestId: existingRefundId,
            BookingId: bookingId,
            BookingReference: "EP-REF-002",
            CustomerId: customerId,
            EventId: Guid.NewGuid(),
            RefundAmount: 100.00m,
            Reason: "Duplicate refund attempt",
            RequestedAt: DateTimeOffset.UtcNow
        );

        // Act
        var result = await service.ProcessRefundAsync(duplicateRefundRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingRefundId, result.Id);

        var count = await dbContext.RefundRecords.CountAsync(r => r.BookingId == bookingId);
        Assert.Equal(1, count); // No duplicate refund record inserted

        var updatedPayment = await dbContext.Payments.FindAsync(payment.Id);
        Assert.Equal(PaymentStatus.Refunded, updatedPayment!.Status);
    }

    [Fact]
    public async Task ProcessRefundAsync_PartialRefund_SetsStatusToPartiallyRefunded()
    {
        // Arrange
        using var dbContext = new PaymentDbContext(_dbOptions);
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = "EP-REF-003",
            CustomerId = customerId,
            Amount = 200.00m,
            Currency = "usd",
            Status = PaymentStatus.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var (service, _, _) = CreateService(dbContext);

        var partialRefundRequest = new BookingRefundRequestedEvent(
            RefundRequestId: Guid.NewGuid(),
            BookingId: bookingId,
            BookingReference: "EP-REF-003",
            CustomerId: customerId,
            EventId: Guid.NewGuid(),
            RefundAmount: 50.00m,
            Reason: "Partial ticket cancellation",
            RequestedAt: DateTimeOffset.UtcNow
        );

        // Act
        var refundRecord = await service.ProcessRefundAsync(partialRefundRequest);

        // Assert
        Assert.NotNull(refundRecord);
        Assert.Equal(50.00m, refundRecord.Amount);

        var updatedPayment = await dbContext.Payments.FindAsync(payment.Id);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatus.PartiallyRefunded, updatedPayment.Status);
    }

    [Fact]
    public async Task KafkaBookingRefundConsumer_ValidMessage_ProcessesRefundSuccessfully()
    {
        // Arrange
        using var dbContext = new PaymentDbContext(_dbOptions);
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            BookingReference = "EP-REF-004",
            CustomerId = customerId,
            Amount = 300.00m,
            Currency = "usd",
            Status = PaymentStatus.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var (service, _, _) = CreateService(dbContext);

        var serviceScopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IPaymentRefundService)))
            .Returns(service);
        serviceScopeMock
            .Setup(s => s.ServiceProvider)
            .Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(serviceScopeMock.Object);

        var consumerLogger = new Mock<ILogger<EventPulse.PaymentService.Consumers.KafkaBookingRefundConsumer>>();
        var consumer = new EventPulse.PaymentService.Consumers.KafkaBookingRefundConsumer(
            Options.Create(new KafkaOptions()),
            scopeFactoryMock.Object,
            consumerLogger.Object
        );

        var refundEvent = new BookingRefundRequestedEvent(
            RefundRequestId: Guid.NewGuid(),
            BookingId: bookingId,
            BookingReference: "EP-REF-004",
            CustomerId: customerId,
            EventId: Guid.NewGuid(),
            RefundAmount: 300.00m,
            Reason: "Full cancellation",
            RequestedAt: DateTimeOffset.UtcNow
        );

        var json = System.Text.Json.JsonSerializer.Serialize(refundEvent);

        // Act
        var success = await consumer.ProcessMessageAsync(json);

        // Assert
        Assert.True(success);
        var updated = await dbContext.Payments.FindAsync(payment.Id);
        Assert.Equal(PaymentStatus.Refunded, updated!.Status);
    }
}
