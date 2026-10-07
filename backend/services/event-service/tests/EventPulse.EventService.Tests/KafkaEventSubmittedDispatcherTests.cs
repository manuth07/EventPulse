using System.Text.Json;
using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Consumers;
using EventPulse.EventService.Data;
using EventPulse.EventService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.EventService.Tests;

public class KafkaEventSubmittedDispatcherTests
{
    private readonly DbContextOptions<EventDbContext> _dbOptions;
    private readonly Mock<IEventSubmittedNotificationHandler> _handlerMock;
    private readonly Mock<ILogger<KafkaEventSubmittedDispatcher>> _loggerMock;
    private readonly KafkaOptions _kafkaOptions;

    public KafkaEventSubmittedDispatcherTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _handlerMock = new Mock<IEventSubmittedNotificationHandler>();
        _loggerMock = new Mock<ILogger<KafkaEventSubmittedDispatcher>>();

        _kafkaOptions = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            Topics = new KafkaTopicOptions
            {
                EventSubmitted = "event-submitted",
                EventSubmittedDlq = "event-submitted-dlq"
            }
        };
    }

    private KafkaEventSubmittedDispatcher CreateDispatcher(EventDbContext db)
    {
        return new KafkaEventSubmittedDispatcher(
            Options.Create(_kafkaOptions),
            db,
            _handlerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DispatchAsync_ValidMessage_InvokesHandlerAndRecordsInbox()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var eventMessageId = Guid.NewGuid();
        var domainEventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        var evt = new EventSubmittedEvent
        {
            EventMessageId = eventMessageId,
            EventId = domainEventId,
            EventTitle = "Spring Music Fest",
            OrganizerId = organizerId,
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };
        var payload = JsonSerializer.Serialize(evt);

        // Act
        var result = await dispatcher.DispatchAsync("event-submitted", domainEventId.ToString(), payload);

        // Assert
        Assert.True(result.IsSuccess);
        _handlerMock.Verify(h => h.HandleAsync(
            It.Is<EventSubmittedEvent>(e => e.EventMessageId == eventMessageId && e.EventId == domainEventId),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify record in inbox
        var inboxRecord = await db.ProcessedIntegrationEvents.FindAsync(eventMessageId);
        Assert.NotNull(inboxRecord);
        Assert.Equal(nameof(EventSubmittedEvent), inboxRecord.EventType);
        Assert.Equal("event-submitted", inboxRecord.Topic);
    }

    [Fact]
    public async Task DispatchAsync_DuplicateMessage_SkipsHandlerAndSucceeds()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var eventMessageId = Guid.NewGuid();
        var domainEventId = Guid.NewGuid();

        // Seed already processed event into inbox
        db.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
        {
            EventId = eventMessageId,
            EventType = nameof(EventSubmittedEvent),
            Topic = "event-submitted",
            ProcessedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var dispatcher = CreateDispatcher(db);

        var evt = new EventSubmittedEvent
        {
            EventMessageId = eventMessageId,
            EventId = domainEventId,
            EventTitle = "Duplicate Event",
            OrganizerId = Guid.NewGuid()
        };
        var payload = JsonSerializer.Serialize(evt);

        // Act
        var result = await dispatcher.DispatchAsync("event-submitted", domainEventId.ToString(), payload);

        // Assert
        Assert.True(result.IsSuccess);
        // Handler must NOT be invoked for duplicate message
        _handlerMock.Verify(h => h.HandleAsync(It.IsAny<EventSubmittedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_MalformedJson_ReturnsNonRetryable()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var malformedPayload = "{ not valid json: true, ...";

        // Act
        var result = await dispatcher.DispatchAsync("event-submitted", "key", malformedPayload);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(result.IsRetryable);
        Assert.Contains("Malformed JSON payload", result.FailureReason);
        _handlerMock.Verify(h => h.HandleAsync(It.IsAny<EventSubmittedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_EmptyOrWhitespacePayload_ReturnsNonRetryable()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        // Act
        var result = await dispatcher.DispatchAsync("event-submitted", "key", "   ");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(result.IsRetryable);
        Assert.Equal("Empty or whitespace payload", result.FailureReason);
    }

    [Fact]
    public async Task DispatchAsync_UnexpectedTopic_ReturnsNonRetryable()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        // Act
        var result = await dispatcher.DispatchAsync("unknown-topic", "key", "{}");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(result.IsRetryable);
        Assert.Contains("Unhandled topic", result.FailureReason);
    }

    [Fact]
    public async Task DispatchAsync_MissingRequiredIdentifiers_ReturnsNonRetryable()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        // Message with Guid.Empty EventId
        var evt = new EventSubmittedEvent
        {
            EventMessageId = Guid.NewGuid(),
            EventId = Guid.Empty, // Missing EventId!
            OrganizerId = Guid.NewGuid(),
            EventTitle = "Invalid Event"
        };
        var payload = JsonSerializer.Serialize(evt);

        // Act
        var result = await dispatcher.DispatchAsync("event-submitted", "key", payload);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(result.IsRetryable);
        Assert.Contains("Missing mandatory identifiers", result.FailureReason);
        _handlerMock.Verify(h => h.HandleAsync(It.IsAny<EventSubmittedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerThrows_ReturnsRetryable()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        _handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<EventSubmittedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Downstream notification service timeout"));

        var evt = new EventSubmittedEvent
        {
            EventMessageId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OrganizerId = Guid.NewGuid(),
            EventTitle = "Test Event"
        };
        var payload = JsonSerializer.Serialize(evt);

        // Act
        var result = await dispatcher.DispatchAsync("event-submitted", evt.EventId.ToString(), payload);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsRetryable);
        Assert.Equal("Downstream notification service timeout", result.FailureReason);
        Assert.Equal(nameof(TimeoutException), result.ExceptionType);

        // Must NOT record in inbox on failure
        var inboxCount = await db.ProcessedIntegrationEvents.CountAsync();
        Assert.Equal(0, inboxCount);
    }
}
