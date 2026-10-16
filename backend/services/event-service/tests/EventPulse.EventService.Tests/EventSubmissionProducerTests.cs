using System.Text.Json;
using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using EventPulse.EventService.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.EventService.Tests;

public class EventSubmissionProducerTests
{
    private readonly DbContextOptions<EventDbContext> _dbOptions;
    private readonly Mock<IEventImageStorage> _storageMock;
    private readonly Mock<ILogger<EventSubmissionService>> _serviceLoggerMock;
    private readonly Mock<ILogger<EventOutboxWriter>> _writerLoggerMock;
    private readonly KafkaOptions _kafkaOptions;

    public EventSubmissionProducerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _storageMock = new Mock<IEventImageStorage>();
        _serviceLoggerMock = new Mock<ILogger<EventSubmissionService>>();
        _writerLoggerMock = new Mock<ILogger<EventOutboxWriter>>();

        _kafkaOptions = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            Topics = new KafkaTopicOptions
            {
                EventSubmitted = "event-submitted"
            }
        };

        // Default storage upload mock
        _storageMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream stream, string contentType, string originalFileName, string folderPrefix, CancellationToken ct) => $"{folderPrefix}/{Guid.NewGuid()}_{originalFileName}");
    }

    private static IFormFile CreateMockFormFile(string fileName = "poster.jpg", string contentType = "image/jpeg", long length = 1024)
    {
        var fileMock = new Mock<IFormFile>();
        var stream = new MemoryStream(new byte[length]);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        return fileMock.Object;
    }

    [Fact]
    public async Task CreateAsync_WhenSuccessful_EnqueuesEventSubmittedIntoOutboxWithCorrectFields()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var outboxWriter = new EventOutboxWriter(db, Options.Create(_kafkaOptions), _writerLoggerMock.Object);
        var service = new EventSubmissionService(db, _storageMock.Object, _serviceLoggerMock.Object, outboxWriter);

        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Title = "Tech Summit 2026",
            Description = "An exciting technology conference exploring AI and distributed systems.",
            Venue = "Grand Ballroom, City Center",
            EventDate = DateTime.UtcNow.AddDays(30),
            Price = 150.00m,
            Category = "Conference",
            VenueType = "Indoor",
            Image = CreateMockFormFile("poster.jpg", "image/jpeg", 2048),
            CoverImage = CreateMockFormFile("cover.jpg", "image/jpeg", 4096)
        };

        // Act
        var (result, error) = await service.CreateAsync(request, organizerId);

        // Assert
        Assert.Null(error);
        Assert.NotNull(result);

        // Verify Event was persisted as Pending
        var createdEvent = await db.Events.FirstOrDefaultAsync(e => e.Id == result.Id);
        Assert.NotNull(createdEvent);
        Assert.Equal(EventStatus.Pending, createdEvent.Status);

        // Verify OutboxMessage was enqueued
        var outboxMessage = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventId != Guid.Empty);
        Assert.NotNull(outboxMessage);
        Assert.Equal(nameof(EventSubmittedEvent), outboxMessage.EventType);
        Assert.Equal("event-submitted", outboxMessage.Topic);
        Assert.Equal(createdEvent.Id.ToString(), outboxMessage.MessageKey);
        Assert.Null(outboxMessage.PublishedAtUtc);
        Assert.Equal(0, outboxMessage.PublishAttempts);

        // Verify deserialized payload
        var evt = JsonSerializer.Deserialize<EventSubmittedEvent>(outboxMessage.Payload);
        Assert.NotNull(evt);
        Assert.Equal(outboxMessage.EventId, evt.EventMessageId);
        Assert.Equal(createdEvent.Id, evt.EventId);
        Assert.Equal("Tech Summit 2026", evt.EventTitle);
        Assert.Equal(organizerId, evt.OrganizerId);
        Assert.Equal(1, evt.EventVersion);
        Assert.True(evt.SubmittedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ResubmitAsync_WhenSuccessful_EnqueuesEventSubmittedIntoOutboxWithCorrectFields()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var outboxWriter = new EventOutboxWriter(db, Options.Create(_kafkaOptions), _writerLoggerMock.Object);
        var service = new EventSubmissionService(db, _storageMock.Object, _serviceLoggerMock.Object, outboxWriter);

        var organizerId = Guid.NewGuid();
        var existingEvent = new Event
        {
            Id = Guid.NewGuid(),
            Title = "Old Rejected Event",
            Description = "Initial description that needs review.",
            Venue = "Hall A",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 50.00m,
            Category = "Music",
            VenueType = "Indoor",
            Status = EventStatus.Rejected,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ImageBlobName = "events/old-poster.jpg"
        };
        db.Events.Add(existingEvent);
        await db.SaveChangesAsync();

        var resubmitRequest = new ResubmitEventRequest
        {
            Title = "Updated & Polished Music Event",
            Description = "Updated description meeting all guidelines.",
            Venue = "Hall A Renovated",
            EventDate = DateTime.UtcNow.AddDays(15),
            Price = 60.00m,
            Category = "Music",
            VenueType = "Indoor"
        };

        // Act
        var (result, error, isNotFound, isForbidden, isInvalidState) = await service.ResubmitAsync(existingEvent.Id, resubmitRequest, organizerId);

        // Assert
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.False(isNotFound);
        Assert.False(isForbidden);
        Assert.False(isInvalidState);

        // Verify status transitioned to Pending
        var updatedEvent = await db.Events.FindAsync(existingEvent.Id);
        Assert.NotNull(updatedEvent);
        Assert.Equal(EventStatus.Pending, updatedEvent.Status);

        // Verify OutboxMessage
        var outboxMessage = await db.OutboxMessages.FirstOrDefaultAsync(m => m.MessageKey == existingEvent.Id.ToString());
        Assert.NotNull(outboxMessage);
        Assert.Equal("event-submitted", outboxMessage.Topic);

        var evt = JsonSerializer.Deserialize<EventSubmittedEvent>(outboxMessage.Payload);
        Assert.NotNull(evt);
        Assert.Equal(existingEvent.Id, evt.EventId);
        Assert.Equal("Updated & Polished Music Event", evt.EventTitle);
        Assert.Equal(organizerId, evt.OrganizerId);
    }

    [Fact]
    public async Task CreateAsync_WhenValidationFails_DoesNotEnqueueEventSubmitted()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var outboxWriter = new EventOutboxWriter(db, Options.Create(_kafkaOptions), _writerLoggerMock.Object);
        var service = new EventSubmissionService(db, _storageMock.Object, _serviceLoggerMock.Object, outboxWriter);

        var request = new CreateEventRequest
        {
            Title = "", // Invalid: empty title
            Description = "Valid description",
            Venue = "Valid venue",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 10m
        };

        // Act
        var (result, error) = await service.CreateAsync(request, Guid.NewGuid());

        // Assert
        Assert.NotNull(error);
        Assert.Null(result);

        // Outbox must be completely empty
        var outboxCount = await db.OutboxMessages.CountAsync();
        Assert.Equal(0, outboxCount);
    }

    [Fact]
    public async Task ResubmitAsync_WhenEventNotRejected_DoesNotEnqueueEventSubmitted()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var outboxWriter = new EventOutboxWriter(db, Options.Create(_kafkaOptions), _writerLoggerMock.Object);
        var service = new EventSubmissionService(db, _storageMock.Object, _serviceLoggerMock.Object, outboxWriter);

        var organizerId = Guid.NewGuid();
        var approvedEvent = new Event
        {
            Id = Guid.NewGuid(),
            Title = "Approved Event",
            Description = "Already approved.",
            Venue = "Hall A",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 50.00m,
            Status = EventStatus.Approved, // Not rejected!
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow
        };
        db.Events.Add(approvedEvent);
        await db.SaveChangesAsync();

        var resubmitRequest = new ResubmitEventRequest
        {
            Title = "Attempted Edit",
            Description = "Some description",
            Venue = "Some venue",
            EventDate = DateTime.UtcNow.AddDays(10),
            Price = 50.00m
        };

        // Act
        var (result, error, isNotFound, isForbidden, isInvalidState) = await service.ResubmitAsync(approvedEvent.Id, resubmitRequest, organizerId);

        // Assert
        Assert.True(isInvalidState);
        Assert.Null(result);

        // Outbox must have 0 messages
        var outboxCount = await db.OutboxMessages.CountAsync();
        Assert.Equal(0, outboxCount);
    }

    [Fact]
    public async Task EventOutboxWriter_EnqueueEventSubmitted_UsesConfiguredTopicAndSerializesPascalCase()
    {
        // Arrange
        using var db = new EventDbContext(_dbOptions);
        var customOptions = new KafkaOptions
        {
            Topics = new KafkaTopicOptions
            {
                EventSubmitted = "custom-event-submitted-topic"
            }
        };
        var writer = new EventOutboxWriter(db, Options.Create(customOptions), _writerLoggerMock.Object);

        var evt = new EventSubmittedEvent
        {
            EventMessageId = Guid.NewGuid(),
            EventVersion = 1,
            SubmittedAtUtc = DateTimeOffset.UtcNow,
            EventId = Guid.NewGuid(),
            EventTitle = "PascalCase Test",
            OrganizerId = Guid.NewGuid()
        };

        // Act
        var message = writer.EnqueueEventSubmitted(evt);
        await db.SaveChangesAsync();

        // Assert
        Assert.Equal("custom-event-submitted-topic", message.Topic);
        Assert.Equal(evt.EventId.ToString(), message.MessageKey);
        Assert.Equal(evt.EventMessageId, message.EventId);

        // Verify PascalCase serialization
        Assert.Contains("\"EventMessageId\"", message.Payload);
        Assert.Contains("\"EventVersion\"", message.Payload);
        Assert.Contains("\"SubmittedAtUtc\"", message.Payload);
        Assert.Contains("\"EventId\"", message.Payload);
        Assert.Contains("\"EventTitle\"", message.Payload);
        Assert.Contains("\"OrganizerId\"", message.Payload);
    }

    [Fact]
    public async Task CreateAsync_WhenDbPersistenceFails_RollsBackAndPerformsCleanup()
    {
        // Arrange
        using var db = new ThrowingEventDbContext(_dbOptions);
        var outboxWriter = new EventOutboxWriter(db, Options.Create(_kafkaOptions), _writerLoggerMock.Object);
        var service = new EventSubmissionService(db, _storageMock.Object, _serviceLoggerMock.Object, outboxWriter);

        var organizerId = Guid.NewGuid();
        var request = new CreateEventRequest
        {
            Title = "Tech Summit 2026",
            Description = "An exciting technology conference exploring AI and distributed systems.",
            Venue = "Grand Ballroom, City Center",
            EventDate = DateTime.UtcNow.AddDays(30),
            Price = 150.00m,
            Category = "Conference",
            VenueType = "Indoor",
            Image = CreateMockFormFile("poster.jpg", "image/jpeg", 2048),
            CoverImage = CreateMockFormFile("cover.jpg", "image/jpeg", 4096)
        };

        // Act
        var (result, error) = await service.CreateAsync(request, organizerId);

        // Assert
        Assert.NotNull(error);
        Assert.Equal("Failed to save event. Please try again.", error);
        Assert.Null(result);

        // Compensating storage cleanup must be invoked for uploaded blobs
        _storageMock.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));

        // In a fresh DbContext on the same in-memory store, verify 0 events and 0 outbox messages were committed
        using var verifyDb = new EventDbContext(_dbOptions);
        Assert.Empty(await verifyDb.Events.ToListAsync());
        Assert.Empty(await verifyDb.OutboxMessages.ToListAsync());
    }

    private class ThrowingEventDbContext : EventDbContext
    {
        public ThrowingEventDbContext(DbContextOptions<EventDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException("Database connection terminated abnormally.", new Exception());
        }
    }
}
