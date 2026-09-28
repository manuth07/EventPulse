using System.Text.Json;
using EventPulse.BookingService.Consumers;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Services;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class KafkaPaymentEventDispatcherTests
{
    private readonly Mock<IPaymentSucceededEventHandler> _succeededHandlerMock;
    private readonly Mock<IPaymentFailedEventHandler> _failedHandlerMock;
    private readonly Mock<ILogger<KafkaPaymentEventDispatcher>> _loggerMock;
    private readonly KafkaOptions _kafkaOptions;
    private readonly IOptions<KafkaOptions> _options;

    public KafkaPaymentEventDispatcherTests()
    {
        _succeededHandlerMock = new Mock<IPaymentSucceededEventHandler>();
        _failedHandlerMock = new Mock<IPaymentFailedEventHandler>();
        _loggerMock = new Mock<ILogger<KafkaPaymentEventDispatcher>>();
        _kafkaOptions = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            ConsumerGroupId = "eventpulse-booking-service",
            Topics = new KafkaTopicOptions
            {
                PaymentSucceeded = "payment-succeeded",
                PaymentFailed = "payment-failed"
            }
        };
        _options = Options.Create(_kafkaOptions);
    }

    private KafkaPaymentEventDispatcher CreateDispatcher()
    {
        return new KafkaPaymentEventDispatcher(
            _options,
            _succeededHandlerMock.Object,
            _failedHandlerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DispatchAsync_ValidPaymentSucceededEvent_InvokesCorrectHandlerAndPreservesProperties()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var payload = new PaymentSucceededEvent
        {
            EventId = eventId,
            EventVersion = 1,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            PaymentId = paymentId,
            BookingId = bookingId,
            BookingReference = "EP-2026-TEST",
            CustomerId = customerId,
            Amount = 4500.00m,
            Currency = "lkr"
        };
        var json = JsonSerializer.Serialize(payload);

        // Act
        var result = await dispatcher.DispatchAsync("payment-succeeded", bookingId.ToString(), json, CancellationToken.None);

        // Assert
        Assert.True(result);
        _succeededHandlerMock.Verify(
            h => h.HandleAsync(
                It.Is<PaymentSucceededEvent>(e =>
                    e.EventId == eventId &&
                    e.BookingId == bookingId &&
                    e.PaymentId == paymentId &&
                    e.CustomerId == customerId &&
                    e.BookingReference == "EP-2026-TEST" &&
                    e.Amount == 4500.00m &&
                    e.Currency == "lkr" &&
                    e.EventVersion == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _failedHandlerMock.Verify(
            h => h.HandleAsync(It.IsAny<PaymentFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_ValidPaymentFailedEvent_InvokesCorrectHandlerAndPreservesProperties()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var payload = new PaymentFailedEvent
        {
            EventId = eventId,
            EventVersion = 1,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            PaymentId = paymentId,
            BookingId = bookingId,
            BookingReference = "EP-2026-FAIL",
            CustomerId = customerId,
            Amount = 3000.00m,
            Currency = "usd",
            FailureCode = "insufficient_funds",
            FailureReason = "Card balance is too low"
        };
        var json = JsonSerializer.Serialize(payload);

        // Act
        var result = await dispatcher.DispatchAsync("payment-failed", bookingId.ToString(), json, CancellationToken.None);

        // Assert
        Assert.True(result);
        _failedHandlerMock.Verify(
            h => h.HandleAsync(
                It.Is<PaymentFailedEvent>(e =>
                    e.EventId == eventId &&
                    e.BookingId == bookingId &&
                    e.PaymentId == paymentId &&
                    e.CustomerId == customerId &&
                    e.BookingReference == "EP-2026-FAIL" &&
                    e.Amount == 3000.00m &&
                    e.Currency == "usd" &&
                    e.FailureCode == "insufficient_funds" &&
                    e.FailureReason == "Card balance is too low" &&
                    e.EventVersion == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _succeededHandlerMock.Verify(
            h => h.HandleAsync(It.IsAny<PaymentSucceededEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_MalformedJson_ReturnsFalseAndDoesNotInvokeHandlers()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var invalidJson = "{ not-valid-json: true ";

        // Act
        var result = await dispatcher.DispatchAsync("payment-succeeded", "test-key", invalidJson, CancellationToken.None);

        // Assert
        Assert.False(result);
        _succeededHandlerMock.Verify(
            h => h.HandleAsync(It.IsAny<PaymentSucceededEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _failedHandlerMock.Verify(
            h => h.HandleAsync(It.IsAny<PaymentFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DispatchAsync_EmptyOrWhitespaceValue_ReturnsFalseAndDoesNotInvokeHandlers(string? emptyValue)
    {
        // Arrange
        var dispatcher = CreateDispatcher();

        // Act
        var result = await dispatcher.DispatchAsync("payment-succeeded", "test-key", emptyValue, CancellationToken.None);

        // Assert
        Assert.False(result);
        _succeededHandlerMock.Verify(
            h => h.HandleAsync(It.IsAny<PaymentSucceededEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_UnknownTopic_ReturnsFalseAndDoesNotInvokeHandlers()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var validJson = JsonSerializer.Serialize(new PaymentSucceededEvent());

        // Act
        var result = await dispatcher.DispatchAsync("unknown-topic", "test-key", validJson, CancellationToken.None);

        // Assert
        Assert.False(result);
        _succeededHandlerMock.Verify(
            h => h.HandleAsync(It.IsAny<PaymentSucceededEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _failedHandlerMock.Verify(
            h => h.HandleAsync(It.IsAny<PaymentFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerThrows_ReturnsFalseAndCatchesException()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        var validJson = JsonSerializer.Serialize(new PaymentSucceededEvent
        {
            BookingId = Guid.NewGuid()
        });

        _succeededHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<PaymentSucceededEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated handler crash"));

        // Act
        var result = await dispatcher.DispatchAsync("payment-succeeded", "test-key", validJson, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task LoggingHandlers_ExecuteWithoutExceptions()
    {
        // Arrange
        var succLogger = new Mock<ILogger<LoggingPaymentSucceededEventHandler>>();
        var succHandler = new LoggingPaymentSucceededEventHandler(succLogger.Object);

        var failLogger = new Mock<ILogger<LoggingPaymentFailedEventHandler>>();
        var failHandler = new LoggingPaymentFailedEventHandler(failLogger.Object);

        var succEvent = new PaymentSucceededEvent
        {
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "lkr"
        };
        var failEvent = new PaymentFailedEvent
        {
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            FailureCode = "err",
            FailureReason = "failed"
        };

        // Act & Assert
        await succHandler.HandleAsync(succEvent);
        await failHandler.HandleAsync(failEvent);
    }
}
