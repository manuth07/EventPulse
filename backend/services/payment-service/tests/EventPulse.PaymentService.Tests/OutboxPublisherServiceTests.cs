using System.Text.Json;
using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.Models;
using EventPulse.PaymentService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.PaymentService.Tests;

public class OutboxPublisherServiceTests
{
    private readonly DbContextOptions<PaymentDbContext> _dbOptions;
    private readonly Mock<IKafkaMessageProducer> _producerMock;
    private readonly Mock<ILogger<OutboxPublisherService>> _loggerMock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _kafkaOptions;

    public OutboxPublisherServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _producerMock = new Mock<IKafkaMessageProducer>();
        _loggerMock = new Mock<ILogger<OutboxPublisherService>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => new PaymentDbContext(_dbOptions));
        var serviceProvider = services.BuildServiceProvider();
        _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _kafkaOptions = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            Topics = new KafkaTopicOptions
            {
                PaymentSucceeded = "payment-succeeded",
                PaymentFailed = "payment-failed"
            },
            Outbox = new KafkaOutboxOptions
            {
                PollingIntervalSeconds = 1,
                BatchSize = 10
            }
        };
    }

    private OutboxPublisherService CreateService()
    {
        return new OutboxPublisherService(
            _scopeFactory,
            _producerMock.Object,
            Options.Create(_kafkaOptions),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessOutboxBatchAsync_WhenUnpublishedMessagesExist_PublishesToKafkaAndUpdatesPublishedAtUtc()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new PaymentSucceededEvent
        {
            EventId = eventId,
            BookingId = bookingId,
            Amount = 5000m
        });

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = nameof(PaymentSucceededEvent),
            Topic = "payment-succeeded",
            MessageKey = bookingId.ToString(),
            Payload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            PublishedAtUtc = null
        };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        _producerMock
            .Setup(p => p.ProduceAsync(
                "payment-succeeded",
                bookingId.ToString(),
                payload,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "payment-succeeded",
                Partition = new Partition(0),
                Offset = new Offset(10)
            });

        var service = CreateService();

        // Act
        var result = await service.ProcessOutboxBatchAsync(10, CancellationToken.None);

        // Assert
        Assert.False(result); // Less than batchSize returned

        using var verifyDb = new PaymentDbContext(_dbOptions);
        var updatedMessage = await verifyDb.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updatedMessage);
        Assert.NotNull(updatedMessage.PublishedAtUtc);
        Assert.Null(updatedMessage.LastError);
        Assert.Equal(0, updatedMessage.PublishAttempts);

        _producerMock.Verify(p => p.ProduceAsync(
            "payment-succeeded",
            bookingId.ToString(),
            payload,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOutboxBatchAsync_WhenKafkaFails_IncrementsPublishAttemptsAndPreservesUnpublishedState()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var eventId = Guid.NewGuid();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = nameof(PaymentSucceededEvent),
            Topic = "payment-succeeded",
            MessageKey = "booking-123",
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PublishedAtUtc = null,
            PublishAttempts = 0
        };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        _producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KafkaException(new Error(ErrorCode.BrokerNotAvailable, "Kafka cluster unavailable")));

        var service = CreateService();

        // Act
        var result = await service.ProcessOutboxBatchAsync(10, CancellationToken.None);

        // Assert
        Assert.False(result);

        using var verifyDb = new PaymentDbContext(_dbOptions);
        var updatedMessage = await verifyDb.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updatedMessage);
        Assert.Null(updatedMessage.PublishedAtUtc);
        Assert.Equal(1, updatedMessage.PublishAttempts);
        Assert.NotNull(updatedMessage.LastError);
        Assert.Contains("Kafka cluster unavailable", updatedMessage.LastError);
    }

    [Fact]
    public async Task ProcessOutboxBatchAsync_WhenRetrySucceeds_UpdatesPublishedAtUtcAndClearsLastError()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var eventId = Guid.NewGuid();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = nameof(PaymentSucceededEvent),
            Topic = "payment-succeeded",
            MessageKey = "booking-retry",
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            PublishedAtUtc = null,
            PublishAttempts = 2,
            LastError = "Previous KafkaException: BrokerNotAvailable"
        };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        _producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "payment-succeeded",
                Partition = new Partition(0),
                Offset = new Offset(25)
            });

        var service = CreateService();

        // Act
        await service.ProcessOutboxBatchAsync(10, CancellationToken.None);

        // Assert
        using var verifyDb = new PaymentDbContext(_dbOptions);
        var updatedMessage = await verifyDb.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updatedMessage);
        Assert.NotNull(updatedMessage.PublishedAtUtc);
        Assert.Null(updatedMessage.LastError);
        Assert.Equal(2, updatedMessage.PublishAttempts);
    }

    [Fact]
    public async Task ProcessOutboxBatchAsync_RespectsBatchSizeAndChronologicalOrdering()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var msgOld = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Topic = "payment-succeeded",
            MessageKey = "1",
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            PublishedAtUtc = null
        };
        var msgMid = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Topic = "payment-succeeded",
            MessageKey = "2",
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            PublishedAtUtc = null
        };
        var msgNew = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Topic = "payment-succeeded",
            MessageKey = "3",
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            PublishedAtUtc = null
        };

        db.OutboxMessages.AddRange(msgOld, msgMid, msgNew);
        await db.SaveChangesAsync();

        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, string>());

        var service = CreateService();

        // Act: Process with batchSize = 2
        var fullBatch = await service.ProcessOutboxBatchAsync(2, CancellationToken.None);

        // Assert: It should report full batch processed
        Assert.True(fullBatch);

        using var verifyDb = new PaymentDbContext(_dbOptions);
        var oldInDb = await verifyDb.OutboxMessages.FindAsync(msgOld.Id);
        var midInDb = await verifyDb.OutboxMessages.FindAsync(msgMid.Id);
        var newInDb = await verifyDb.OutboxMessages.FindAsync(msgNew.Id);

        Assert.NotNull(oldInDb!.PublishedAtUtc);
        Assert.NotNull(midInDb!.PublishedAtUtc);
        // The newest message should NOT have been published in the first batch of 2
        Assert.Null(newInDb!.PublishedAtUtc);
    }

    [Fact]
    public void OutboxWriter_Enqueue_StoresIdenticalEventIdInMessageAndPayload()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var logger = new Mock<ILogger<OutboxWriter>>();
        var writer = new OutboxWriter(db, Options.Create(_kafkaOptions), logger.Object);

        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentEvent = new PaymentSucceededEvent
        {
            EventId = eventId,
            BookingId = bookingId,
            Amount = 3500m,
            Currency = "lkr"
        };

        // Act
        var outboxMessage = writer.EnqueuePaymentSucceeded(paymentEvent);

        // Assert
        Assert.Equal(eventId, outboxMessage.EventId);
        Assert.Equal("payment-succeeded", outboxMessage.Topic);
        Assert.Equal(bookingId.ToString(), outboxMessage.MessageKey);
        Assert.Null(outboxMessage.PublishedAtUtc);

        var deserialized = JsonSerializer.Deserialize<PaymentSucceededEvent>(outboxMessage.Payload);
        Assert.NotNull(deserialized);
        Assert.Equal(eventId, deserialized.EventId);
        Assert.Equal(bookingId, deserialized.BookingId);
        Assert.Equal(3500m, deserialized.Amount);
    }

    [Fact]
    public async Task CleanupExpiredPublishedMessagesAsync_RemovesOldPublishedRows_KeepsRecentPublished_AndNeverRemovesUnpublished()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        var retentionDays = 7;
        _kafkaOptions.Outbox.RetentionDays = retentionDays;

        var now = DateTimeOffset.UtcNow;

        // 1. Old published message (older than 7 days) -> SHOULD BE REMOVED
        var oldPublished = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Topic = "payment-succeeded",
            MessageKey = "old-pub",
            Payload = "{}",
            CreatedAtUtc = now.AddDays(-10),
            PublishedAtUtc = now.AddDays(-8)
        };

        // 2. Recent published message (within 7 days) -> MUST BE PRESERVED
        var recentPublished = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Topic = "payment-succeeded",
            MessageKey = "recent-pub",
            Payload = "{}",
            CreatedAtUtc = now.AddDays(-2),
            PublishedAtUtc = now.AddDays(-1)
        };

        // 3. Very old UNPUBLISHED message (awaiting retry) -> MUST NEVER BE REMOVED
        var oldUnpublished = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Topic = "payment-succeeded",
            MessageKey = "old-unpub",
            Payload = "{}",
            CreatedAtUtc = now.AddDays(-30),
            PublishedAtUtc = null,
            PublishAttempts = 5
        };

        // 4. Recent UNPUBLISHED message -> MUST NEVER BE REMOVED
        var recentUnpublished = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Topic = "payment-succeeded",
            MessageKey = "recent-unpub",
            Payload = "{}",
            CreatedAtUtc = now.AddMinutes(-10),
            PublishedAtUtc = null
        };

        db.OutboxMessages.AddRange(oldPublished, recentPublished, oldUnpublished, recentUnpublished);
        await db.SaveChangesAsync();

        var service = CreateService();

        // Act
        var deletedCount = await service.CleanupExpiredPublishedMessagesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, deletedCount);

        using var verifyDb = new PaymentDbContext(_dbOptions);
        var remainingMessages = await verifyDb.OutboxMessages.ToListAsync();

        Assert.Equal(3, remainingMessages.Count);
        Assert.DoesNotContain(remainingMessages, m => m.Id == oldPublished.Id);
        Assert.Contains(remainingMessages, m => m.Id == recentPublished.Id);
        Assert.Contains(remainingMessages, m => m.Id == oldUnpublished.Id);
        Assert.Contains(remainingMessages, m => m.Id == recentUnpublished.Id);
    }

    [Fact]
    public async Task CleanupExpiredPublishedMessagesAsync_WhenNoExpiredMessages_ReturnsZero()
    {
        // Arrange
        using var db = new PaymentDbContext(_dbOptions);
        _kafkaOptions.Outbox.RetentionDays = 7;

        var recentPublished = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Topic = "payment-succeeded",
            MessageKey = "recent",
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            PublishedAtUtc = DateTimeOffset.UtcNow.AddHours(-12)
        };
        db.OutboxMessages.Add(recentPublished);
        await db.SaveChangesAsync();

        var service = CreateService();

        // Act
        var deletedCount = await service.CleanupExpiredPublishedMessagesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, deletedCount);

        using var verifyDb = new PaymentDbContext(_dbOptions);
        Assert.Equal(1, await verifyDb.OutboxMessages.CountAsync());
    }

    [Fact]
    public void KafkaOutboxOptions_DefaultValues_ReflectProductionHardeningSettings()
    {
        // Act
        var options = new KafkaOutboxOptions();

        // Assert
        Assert.Equal(3, options.PollingIntervalSeconds);
        Assert.Equal(50, options.BatchSize);
        Assert.Equal(7, options.RetentionDays);
        Assert.Equal(60, options.CleanupIntervalMinutes);
    }
}
