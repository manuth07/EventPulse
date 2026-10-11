using System.Text.Json;
using EventPulse.Contracts.Kafka;
using EventPulse.Contracts.Kafka.Events;
using EventPulse.EventService.Consumers;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.EventService.Tests;

public class KafkaBookingCancelledDispatcherTests
{
    private readonly DbContextOptions<EventDbContext> _dbOptions;
    private readonly Mock<IEventInventoryService> _inventoryServiceMock;
    private readonly Mock<ILogger<KafkaBookingCancelledDispatcher>> _loggerMock;
    private readonly KafkaOptions _kafkaOptions;

    public KafkaBookingCancelledDispatcherTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _inventoryServiceMock = new Mock<IEventInventoryService>();
        _loggerMock = new Mock<ILogger<KafkaBookingCancelledDispatcher>>();

        _kafkaOptions = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            Topics = new KafkaTopicOptions
            {
                BookingCancelled = "booking-cancelled",
                BookingCancelledDlq = "booking-cancelled-dlq"
            }
        };
    }

    private KafkaBookingCancelledDispatcher CreateDispatcher(EventDbContext db)
    {
        return new KafkaBookingCancelledDispatcher(
            Options.Create(_kafkaOptions),
            db,
            _inventoryServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DispatchAsync_ValidMessage_RestoresInventoryAndRecordsInbox()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var eventMessageId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        var evt = new BookingCancelledIntegrationEvent
        {
            EventMessageId = eventMessageId,
            BookingId = bookingId,
            BookingReference = "BK-12345",
            EventId = eventId,
            ReleasedTickets = new List<CancelledTicketItem>
            {
                new(ticketTypeId, 2)
            }
        };
        var payload = JsonSerializer.Serialize(evt);

        _inventoryServiceMock
            .Setup(s => s.RestoreInventoryAsync(eventId, It.IsAny<RestoreInventoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InventoryOperationResult.Succeeded());

        // Act
        var result = await dispatcher.DispatchAsync("booking-cancelled", bookingId.ToString(), payload);

        // Assert
        Assert.True(result.IsSuccess);
        _inventoryServiceMock.Verify(s => s.RestoreInventoryAsync(
            eventId,
            It.Is<RestoreInventoryRequest>(r =>
                r.BookingId == bookingId &&
                r.CancellationKey == $"booking-{bookingId}-cancelled" &&
                r.Items.Count == 1 &&
                r.Items[0].TicketTypeId == ticketTypeId &&
                r.Items[0].Quantity == 2),
            It.IsAny<CancellationToken>()), Times.Once);

        var inboxRecord = await db.ProcessedIntegrationEvents.FindAsync(eventMessageId);
        Assert.NotNull(inboxRecord);
        Assert.Equal(nameof(BookingCancelledIntegrationEvent), inboxRecord.EventType);
        Assert.Equal("booking-cancelled", inboxRecord.Topic);
    }

    [Fact]
    public async Task DispatchAsync_DuplicateMessage_SkipsInventoryRestorationAndReturnsSuccess()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var eventMessageId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        db.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
        {
            EventId = eventMessageId,
            EventType = nameof(BookingCancelledIntegrationEvent),
            Topic = "booking-cancelled",
            ProcessedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var evt = new BookingCancelledIntegrationEvent
        {
            EventMessageId = eventMessageId,
            BookingId = bookingId,
            EventId = Guid.NewGuid(),
            ReleasedTickets = new List<CancelledTicketItem> { new(Guid.NewGuid(), 1) }
        };
        var payload = JsonSerializer.Serialize(evt);

        // Act
        var result = await dispatcher.DispatchAsync("booking-cancelled", bookingId.ToString(), payload);

        // Assert
        Assert.True(result.IsSuccess);
        _inventoryServiceMock.Verify(s => s.RestoreInventoryAsync(
            It.IsAny<Guid>(), It.IsAny<RestoreInventoryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_EmptyOrWhitespacePayload_ReturnsNonRetryable()
    {
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var result = await dispatcher.DispatchAsync("booking-cancelled", "key", "   ");

        Assert.False(result.IsSuccess);
        Assert.False(result.IsRetryable);
        Assert.Contains("Empty or whitespace", result.FailureReason);
    }

    [Fact]
    public async Task DispatchAsync_UnhandledTopic_ReturnsNonRetryable()
    {
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var result = await dispatcher.DispatchAsync("unknown-topic", "key", "{}");

        Assert.False(result.IsSuccess);
        Assert.False(result.IsRetryable);
        Assert.Contains("Unhandled topic", result.FailureReason);
    }

    [Fact]
    public async Task DispatchAsync_MalformedJson_ReturnsNonRetryable()
    {
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var result = await dispatcher.DispatchAsync("booking-cancelled", "key", "{ not-valid-json ]");

        Assert.False(result.IsSuccess);
        Assert.False(result.IsRetryable);
        Assert.Contains("Malformed JSON", result.FailureReason);
    }

    [Fact]
    public async Task DispatchAsync_MissingMandatoryIdentifiers_ReturnsNonRetryable()
    {
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var evt = new BookingCancelledIntegrationEvent
        {
            BookingId = Guid.Empty, // Missing BookingId
            EventId = Guid.NewGuid()
        };
        var payload = JsonSerializer.Serialize(evt);

        var result = await dispatcher.DispatchAsync("booking-cancelled", "key", payload);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsRetryable);
        Assert.Contains("Missing mandatory identifiers", result.FailureReason);
    }

    [Fact]
    public async Task DispatchAsync_EventInPast_BypassesRestorationAndRecordsProcessed()
    {
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var eventMessageId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var evt = new BookingCancelledIntegrationEvent
        {
            EventMessageId = eventMessageId,
            BookingId = bookingId,
            EventId = eventId,
            ReleasedTickets = new List<CancelledTicketItem> { new(Guid.NewGuid(), 1) }
        };
        var payload = JsonSerializer.Serialize(evt);

        _inventoryServiceMock
            .Setup(s => s.RestoreInventoryAsync(eventId, It.IsAny<RestoreInventoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InventoryOperationResult.Failed("Cannot restore inventory for an event that has already started or concluded.", 400));

        var result = await dispatcher.DispatchAsync("booking-cancelled", bookingId.ToString(), payload);

        // Per policy, past events return success so message is marked processed and not sent to DLQ
        Assert.True(result.IsSuccess);

        var inboxRecord = await db.ProcessedIntegrationEvents.FindAsync(eventMessageId);
        Assert.NotNull(inboxRecord);
    }

    [Fact]
    public async Task DispatchAsync_FallbackToItemsIfReleasedTicketsEmpty_Succeeds()
    {
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var eventMessageId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        var evt = new BookingCancelledIntegrationEvent
        {
            EventMessageId = eventMessageId,
            BookingId = bookingId,
            EventId = eventId,
            ReleasedTickets = new List<CancelledTicketItem>(), // empty
            Items = new List<BookingCancelledItem>
            {
                new() { TicketTypeId = ticketTypeId, TicketName = "Standard", Quantity = 3, UnitPrice = 50, Subtotal = 150 }
            }
        };
        var payload = JsonSerializer.Serialize(evt);

        _inventoryServiceMock
            .Setup(s => s.RestoreInventoryAsync(eventId, It.IsAny<RestoreInventoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InventoryOperationResult.Succeeded());

        var result = await dispatcher.DispatchAsync("booking-cancelled", bookingId.ToString(), payload);

        Assert.True(result.IsSuccess);
        _inventoryServiceMock.Verify(s => s.RestoreInventoryAsync(
            eventId,
            It.Is<RestoreInventoryRequest>(r => r.Items.Count == 1 && r.Items[0].Quantity == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_TransientFailure_ReturnsRetryable()
    {
        using var db = new EventDbContext(_dbOptions);
        var dispatcher = CreateDispatcher(db);

        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var evt = new BookingCancelledIntegrationEvent
        {
            BookingId = bookingId,
            EventId = eventId,
            ReleasedTickets = new List<CancelledTicketItem> { new(Guid.NewGuid(), 1) }
        };
        var payload = JsonSerializer.Serialize(evt);

        _inventoryServiceMock
            .Setup(s => s.RestoreInventoryAsync(eventId, It.IsAny<RestoreInventoryRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database connection timeout"));

        var result = await dispatcher.DispatchAsync("booking-cancelled", bookingId.ToString(), payload);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsRetryable);
        Assert.Equal(nameof(TimeoutException), result.ExceptionType);
    }
}
