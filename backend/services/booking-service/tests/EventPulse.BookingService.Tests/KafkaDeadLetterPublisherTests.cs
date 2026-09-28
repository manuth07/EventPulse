using System.Text.Json;
using Confluent.Kafka;
using EventPulse.BookingService.Consumers;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class KafkaDeadLetterPublisherTests
{
    private readonly Mock<IProducer<string, string>> _producerMock;
    private readonly Mock<ILogger<KafkaDeadLetterPublisher>> _loggerMock;
    private readonly KafkaOptions _kafkaOptions;

    public KafkaDeadLetterPublisherTests()
    {
        _producerMock = new Mock<IProducer<string, string>>();
        _loggerMock = new Mock<ILogger<KafkaDeadLetterPublisher>>();
        _kafkaOptions = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            Topics = new KafkaTopicOptions
            {
                PaymentSucceeded = "payment-succeeded",
                PaymentFailed = "payment-failed",
                PaymentSucceededDlq = "payment-succeeded-dlq",
                PaymentFailedDlq = "payment-failed-dlq"
            }
        };
    }

    [Fact]
    public async Task PublishDeadLetterAsync_PaymentSucceededFailure_RoutesToPaymentSucceededDlq()
    {
        // Arrange
        var publisher = new KafkaDeadLetterPublisher(
            Options.Create(_kafkaOptions),
            _loggerMock.Object,
            _producerMock.Object);

        string? capturedTopic = null;
        Message<string, string>? capturedMessage = null;

        _producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((topic, msg, _) =>
            {
                capturedTopic = topic;
                capturedMessage = msg;
            })
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "payment-succeeded-dlq",
                Partition = new Partition(0),
                Offset = new Offset(10)
            });

        var deadLetter = new DeadLetterMessage
        {
            OriginalTopic = "payment-succeeded",
            OriginalPartition = 0,
            OriginalOffset = 42,
            MessageKey = "booking-123",
            Payload = "{\"bad\": \"data\"}",
            FailureReason = "Malformed JSON",
            ExceptionType = "JsonException",
            AttemptCount = 1,
            FailedAtUtc = DateTimeOffset.UtcNow
        };

        // Act
        await publisher.PublishDeadLetterAsync(deadLetter);

        // Assert
        Assert.Equal("payment-succeeded-dlq", capturedTopic);
        Assert.NotNull(capturedMessage);
        Assert.Equal("booking-123", capturedMessage.Key);

        var deserialized = JsonSerializer.Deserialize<DeadLetterMessage>(capturedMessage.Value);
        Assert.NotNull(deserialized);
        Assert.Equal("payment-succeeded", deserialized.OriginalTopic);
        Assert.Equal(42, deserialized.OriginalOffset);
        Assert.Equal("Malformed JSON", deserialized.FailureReason);
        Assert.Equal("JsonException", deserialized.ExceptionType);
        Assert.Equal(1, deserialized.AttemptCount);
    }

    [Fact]
    public async Task PublishDeadLetterAsync_PaymentFailedFailure_RoutesToPaymentFailedDlq()
    {
        // Arrange
        var publisher = new KafkaDeadLetterPublisher(
            Options.Create(_kafkaOptions),
            _loggerMock.Object,
            _producerMock.Object);

        string? capturedTopic = null;

        _producerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((topic, _, _) =>
            {
                capturedTopic = topic;
            })
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "payment-failed-dlq",
                Partition = new Partition(0),
                Offset = new Offset(5)
            });

        var deadLetter = new DeadLetterMessage
        {
            OriginalTopic = "payment-failed",
            OriginalPartition = 0,
            OriginalOffset = 15,
            MessageKey = "booking-456",
            Payload = "{}"
        };

        // Act
        await publisher.PublishDeadLetterAsync(deadLetter);

        // Assert
        Assert.Equal("payment-failed-dlq", capturedTopic);
    }
}
