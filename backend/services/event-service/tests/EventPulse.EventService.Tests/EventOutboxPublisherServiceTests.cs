using System.Text.Json;
using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Data;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.EventService.Tests;

public class EventOutboxPublisherServiceTests
{
    private readonly DbContextOptions<EventDbContext> _dbOptions;
    private readonly Mock<IKafkaMessageProducer> _producerMock;
    private readonly Mock<ILogger<EventOutboxPublisherService>> _loggerMock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _kafkaOptions;

    public EventOutboxPublisherServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _producerMock = new Mock<IKafkaMessageProducer>();
        _loggerMock = new Mock<ILogger<EventOutboxPublisherService>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => new EventDbContext(_dbOptions));
        var serviceProvider = services.BuildServiceProvider();
        _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _kafkaOptions = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            Topics = new KafkaTopicOptions
            {
                EventSubmitted = "event-submitted"
            },
            Outbox = new KafkaOutboxOptions
            {
                PollingIntervalSeconds = 1,
                BatchSize = 10,
                RetentionDays = 7
            }
        };
    }

    private EventOutboxPublisherService CreateService()
    {
        return new EventOutboxPublisherService(
            _scopeFactory,
            _producerMock.Object,
            Options.Create(_kafkaOptions),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessOutboxBatchAsync_WhenUnpublishedMessagesExist_PublishesToKafkaAndUpdatesPublishedAtUtc()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var eventId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        var evt = new EventSubmittedEvent
        {
            EventMessageId = messageId,
            EventId = eventId,
            EventTitle = "Jazz in the Park",
            OrganizerId = organizerId,
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };
        var payload = JsonSerializer.Serialize(evt);

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = messageId,
            EventType = nameof(EventSubmittedEvent),
            Topic = "event-submitted",
            MessageKey = eventId.ToString(),
            Payload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            PublishedAtUtc = null,
            PublishAttempts = 0
        };

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        _producerMock
            .Setup(p => p.ProduceAsync(
                "event-submitted",
                eventId.ToString(),
                payload,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "event-submitted",
                Partition = new Partition(0),
                Offset = new Offset(42),
                Status = PersistenceStatus.Persisted
            });

        var service = CreateService();

        // Act
        var result = await service.ProcessOutboxBatchAsync(10, CancellationToken.None);

        // Assert
        Assert.False(result); // batch size 10, processed 1 message, so returns false (no more in batch)

        using var verifyDb = new EventDbContext(_dbOptions);
        var updated = await verifyDb.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.PublishedAtUtc);
        Assert.Null(updated.LastError);

        _producerMock.Verify(p => p.ProduceAsync(
            "event-submitted",
            eventId.ToString(),
            payload,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOutboxBatchAsync_WhenKafkaProducerThrows_IncrementsPublishAttemptsAndSetsLastError()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var eventId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = messageId,
            EventType = nameof(EventSubmittedEvent),
            Topic = "event-submitted",
            MessageKey = eventId.ToString(),
            Payload = "{\"EventTitle\":\"Test\"}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
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
            .ThrowsAsync(new InvalidOperationException("Kafka broker unreachable"));

        var service = CreateService();

        // Act
        var result = await service.ProcessOutboxBatchAsync(10, CancellationToken.None);

        // Assert
        Assert.False(result);

        using var verifyDb = new EventDbContext(_dbOptions);
        var updated = await verifyDb.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updated);
        Assert.Null(updated.PublishedAtUtc); // Still UNPUBLISHED
        Assert.Equal(1, updated.PublishAttempts);
        Assert.Contains("Kafka broker unreachable", updated.LastError);
    }

    [Fact]
    public async Task ProcessOutboxBatchAsync_WhenNoUnpublishedMessages_ReturnsFalseWithoutCallingProducer()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        // Add an already published message
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = nameof(EventSubmittedEvent),
            Topic = "event-submitted",
            MessageKey = Guid.NewGuid().ToString(),
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
            PublishedAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.ProcessOutboxBatchAsync(10, CancellationToken.None);

        // Assert
        Assert.False(result);
        _producerMock.Verify(p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupExpiredPublishedMessagesAsync_RemovesOnlyExpiredPublishedMessages_NeverUnpublished()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);

        // 1. Expired published message (older than retention = 7 days)
        var expiredPublished = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = nameof(EventSubmittedEvent),
            Topic = "event-submitted",
            MessageKey = Guid.NewGuid().ToString(),
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-9)
        };

        // 2. Recent published message (within retention)
        var recentPublished = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = nameof(EventSubmittedEvent),
            Topic = "event-submitted",
            MessageKey = Guid.NewGuid().ToString(),
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-2)
        };

        // 3. Very old UNPUBLISHED message (must NEVER be deleted)
        var oldUnpublished = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = nameof(EventSubmittedEvent),
            Topic = "event-submitted",
            MessageKey = Guid.NewGuid().ToString(),
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
            PublishedAtUtc = null
        };

        db.OutboxMessages.AddRange(expiredPublished, recentPublished, oldUnpublished);
        await db.SaveChangesAsync();

        var service = CreateService();

        // Act
        var cleanedCount = await service.CleanupExpiredPublishedMessagesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, cleanedCount);

        using var verifyDb = new EventDbContext(_dbOptions);
        var remainingMessages = await verifyDb.OutboxMessages.ToListAsync();

        Assert.Equal(2, remainingMessages.Count);
        Assert.DoesNotContain(remainingMessages, m => m.Id == expiredPublished.Id);
        Assert.Contains(remainingMessages, m => m.Id == recentPublished.Id);
        Assert.Contains(remainingMessages, m => m.Id == oldUnpublished.Id); // UNPUBLISHED message preserved!
    }
}
