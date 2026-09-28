using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using EventPulse.Contracts.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class PaymentFailedEventHandlerTests
{
    private static BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BookingDbContext(options);
    }

    private static (PaymentFailedEventHandler handler, Mock<IBookingPaymentFailureService> failureServiceMock, Mock<ILogger<PaymentFailedEventHandler>> loggerMock)
        CreateHandler(BookingDbContext context)
    {
        var failureServiceMock = new Mock<IBookingPaymentFailureService>();
        var loggerMock = new Mock<ILogger<PaymentFailedEventHandler>>();
        var handler = new PaymentFailedEventHandler(context, failureServiceMock.Object, loggerMock.Object);
        return (handler, failureServiceMock, loggerMock);
    }

    [Fact]
    public async Task HandleAsync_WhenValidPaymentFailedEvent_TransitionsBookingAndRecordsInboxEvent()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (handler, failureServiceMock, _) = CreateHandler(context);

        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-FAIL-HDL-01",
            Status = BookingStatus.PaymentFailed
        };

        failureServiceMock
            .Setup(s => s.HandlePaymentFailureAsync(bookingId, "card_declined", "Declined", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingPaymentFailureResult.Failed(booking));

        var paymentEvent = new PaymentFailedEvent
        {
            EventId = eventId,
            BookingId = bookingId,
            PaymentId = paymentId,
            FailureCode = "card_declined",
            FailureReason = "Declined"
        };

        // Act
        await handler.HandleAsync(paymentEvent, CancellationToken.None);

        // Assert
        failureServiceMock.Verify(s => s.HandlePaymentFailureAsync(bookingId, "card_declined", "Declined", It.IsAny<CancellationToken>()), Times.Once);

        var inboxRecord = await context.ProcessedIntegrationEvents.FirstOrDefaultAsync(e => e.EventId == eventId);
        Assert.NotNull(inboxRecord);
        Assert.Equal(nameof(PaymentFailedEvent), inboxRecord.EventType);
        Assert.Equal(KafkaTopics.PaymentFailed, inboxRecord.Topic);
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateEventId_SkipsProcessingAndCallsNoFailureService()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (handler, failureServiceMock, _) = CreateHandler(context);

        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        // Seed existing inbox entry
        context.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
        {
            EventId = eventId,
            EventType = nameof(PaymentFailedEvent),
            Topic = KafkaTopics.PaymentFailed,
            ProcessedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await context.SaveChangesAsync();

        var paymentEvent = new PaymentFailedEvent
        {
            EventId = eventId,
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            FailureCode = "card_declined",
            FailureReason = "Duplicate event"
        };

        // Act
        await handler.HandleAsync(paymentEvent, CancellationToken.None);

        // Assert - Failure service was never called due to inbox deduplication
        failureServiceMock.Verify(
            s => s.HandlePaymentFailureAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingAlreadyConfirmed_SafelyAcknowledgesAndRecordsInboxEvent()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (handler, failureServiceMock, loggerMock) = CreateHandler(context);

        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = bookingId,
            BookingReference = "EP-CONFIRMED-01",
            Status = BookingStatus.Confirmed
        };

        failureServiceMock
            .Setup(s => s.HandlePaymentFailureAsync(bookingId, "late_err", "Late failure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingPaymentFailureResult.AlreadyConfirmed(booking));

        var paymentEvent = new PaymentFailedEvent
        {
            EventId = eventId,
            BookingId = bookingId,
            PaymentId = Guid.NewGuid(),
            FailureCode = "late_err",
            FailureReason = "Late failure"
        };

        // Act
        await handler.HandleAsync(paymentEvent, CancellationToken.None);

        // Assert
        var inboxRecord = await context.ProcessedIntegrationEvents.FirstOrDefaultAsync(e => e.EventId == eventId);
        Assert.NotNull(inboxRecord);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already Confirmed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (handler, failureServiceMock, _) = CreateHandler(context);

        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        failureServiceMock
            .Setup(s => s.HandlePaymentFailureAsync(bookingId, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingPaymentFailureResult.NotFound(bookingId));

        var paymentEvent = new PaymentFailedEvent
        {
            EventId = eventId,
            BookingId = bookingId,
            PaymentId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(paymentEvent, CancellationToken.None));

        // Inbox record should NOT be created for failed processing
        var inboxRecord = await context.ProcessedIntegrationEvents.FirstOrDefaultAsync(e => e.EventId == eventId);
        Assert.Null(inboxRecord);
    }
}
