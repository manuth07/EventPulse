using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Consumers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.EventService.Tests;

public class KafkaBookingCancelledConsumerTests
{
    private readonly Mock<IKafkaBookingCancelledDispatcher> _dispatcherMock;
    private readonly Mock<IDeadLetterPublisher> _deadLetterPublisherMock;
    private readonly Mock<ILogger<KafkaBookingCancelledConsumer>> _consumerLoggerMock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _kafkaOptions;

    public KafkaBookingCancelledConsumerTests()
    {
        _dispatcherMock = new Mock<IKafkaBookingCancelledDispatcher>();
        _deadLetterPublisherMock = new Mock<IDeadLetterPublisher>();
        _consumerLoggerMock = new Mock<ILogger<KafkaBookingCancelledConsumer>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => _dispatcherMock.Object);
        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        _kafkaOptions = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            ConsumerGroupId = "eventpulse-event-service",
            Topics = new KafkaTopicOptions
            {
                BookingCancelled = "booking-cancelled",
                BookingCancelledDlq = "booking-cancelled-dlq"
            },
            Consumer = new KafkaConsumerOptions
            {
                MaxProcessingAttempts = 3,
                RetryBaseDelaySeconds = 0.01 // Fast retries in test
            }
        };
    }

    private KafkaBookingCancelledConsumer CreateConsumer()
    {
        return new KafkaBookingCancelledConsumer(
            Options.Create(_kafkaOptions),
            _scopeFactory,
            _deadLetterPublisherMock.Object,
            _consumerLoggerMock.Object);
    }

    private static ConsumeResult<string, string> CreateConsumeResult(string topic, string key, string value, int partition = 0, long offset = 100)
    {
        return new ConsumeResult<string, string>
        {
            Topic = topic,
            Partition = new Partition(partition),
            Offset = new Offset(offset),
            Message = new Message<string, string>
            {
                Key = key,
                Value = value
            }
        };
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenDispatcherSucceeds_CommitsOffsetAndDoesNotCallDlq()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("booking-cancelled", "booking-123", "{\"BookingId\":\"00000000-0000-0000-0000-000000000001\"}");
        bool committed = false;

        _dispatcherMock
            .Setup(d => d.DispatchAsync("booking-cancelled", "booking-123", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.Success());

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => committed = true);

        // Assert
        Assert.True(result);
        Assert.True(committed);
        _deadLetterPublisherMock.Verify(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenNonRetryableFailure_RoutesToDlqAndCommitsOffset()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("booking-cancelled", "bad-key", "invalid payload");
        bool committed = false;

        _dispatcherMock
            .Setup(d => d.DispatchAsync("booking-cancelled", "bad-key", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.NonRetryable("Malformed JSON payload"));

        _deadLetterPublisherMock
            .Setup(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => committed = true);

        // Assert
        Assert.True(result);
        Assert.True(committed);
        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _deadLetterPublisherMock.Verify(d => d.PublishDeadLetterAsync(
            It.Is<DeadLetterMessage>(m =>
                m.OriginalTopic == "booking-cancelled" &&
                m.FailureReason.Contains("Malformed") &&
                m.AttemptCount == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenTransientFailureEventuallySucceeds_RetriesAndCommitsOffset()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("booking-cancelled", "retry-key", "{\"BookingId\":\"00000000-0000-0000-0000-000000000002\"}");
        bool committed = false;

        _dispatcherMock
            .SetupSequence(d => d.DispatchAsync("booking-cancelled", "retry-key", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.Retryable("DB lock timeout"))
            .ReturnsAsync(EventDispatchResult.Success());

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => committed = true);

        // Assert
        Assert.True(result);
        Assert.True(committed);
        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _deadLetterPublisherMock.Verify(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenMaxAttemptsExceeded_RoutesToDlqAndCommitsOffset()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("booking-cancelled", "exhaust-key", "{\"BookingId\":\"00000000-0000-0000-0000-000000000003\"}");
        bool committed = false;

        _dispatcherMock
            .Setup(d => d.DispatchAsync("booking-cancelled", "exhaust-key", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.Retryable("Persistent DB error"));

        _deadLetterPublisherMock
            .Setup(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => committed = true);

        // Assert
        Assert.True(result);
        Assert.True(committed);
        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _deadLetterPublisherMock.Verify(d => d.PublishDeadLetterAsync(
            It.Is<DeadLetterMessage>(m => m.AttemptCount == 3 && m.FailureReason == "Persistent DB error"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenDlqFails_DoesNotCommitOffset()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("booking-cancelled", "bad-key", "invalid payload");
        bool committed = false;

        _dispatcherMock
            .Setup(d => d.DispatchAsync("booking-cancelled", "bad-key", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.NonRetryable("Corrupted record"));

        _deadLetterPublisherMock
            .Setup(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Kafka DLQ topic unreachable"));

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => committed = true);

        // Assert
        Assert.False(result);
        Assert.False(committed);
    }
}
