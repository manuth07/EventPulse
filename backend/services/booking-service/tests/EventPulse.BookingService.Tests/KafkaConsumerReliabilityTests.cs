using Confluent.Kafka;
using EventPulse.BookingService.Consumers;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests;

public class KafkaConsumerReliabilityTests
{
    private readonly Mock<IKafkaPaymentEventDispatcher> _dispatcherMock;
    private readonly Mock<IDeadLetterPublisher> _deadLetterPublisherMock;
    private readonly Mock<ILogger<KafkaPaymentEventConsumer>> _loggerMock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _kafkaOptions;

    public KafkaConsumerReliabilityTests()
    {
        _dispatcherMock = new Mock<IKafkaPaymentEventDispatcher>();
        _deadLetterPublisherMock = new Mock<IDeadLetterPublisher>();
        _loggerMock = new Mock<ILogger<KafkaPaymentEventConsumer>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => _dispatcherMock.Object);
        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        _kafkaOptions = new KafkaOptions
        {
            BootstrapServers = "localhost:9092",
            ConsumerGroupId = "test-group",
            Topics = new KafkaTopicOptions
            {
                PaymentSucceeded = "payment-succeeded",
                PaymentFailed = "payment-failed",
                PaymentSucceededDlq = "payment-succeeded-dlq",
                PaymentFailedDlq = "payment-failed-dlq"
            },
            Consumer = new KafkaConsumerOptions
            {
                MaxProcessingAttempts = 3,
                RetryBaseDelaySeconds = 0.01 // Fast delay for unit tests
            }
        };
    }

    private KafkaPaymentEventConsumer CreateConsumer()
    {
        return new KafkaPaymentEventConsumer(
            Options.Create(_kafkaOptions),
            _scopeFactory,
            _deadLetterPublisherMock.Object,
            _loggerMock.Object);
    }

    private static ConsumeResult<string, string> CreateConsumeResult(string topic, string payload, long offset = 10, int partition = 0)
    {
        return new ConsumeResult<string, string>
        {
            Topic = topic,
            Partition = new Partition(partition),
            Offset = new Offset(offset),
            Message = new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = payload
            }
        };
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenDispatchSucceedsFirstTime_CommitsOffsetAndDoesNotCallDlq()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("payment-succeeded", "{\"valid\": true}");

        _dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.Success());

        bool commitCalled = false;

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => commitCalled = true);

        // Assert
        Assert.True(result);
        Assert.True(commitCalled);
        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _deadLetterPublisherMock.Verify(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenFirstAttemptFailsAndSecondSucceeds_RetriesAndCommitsOffset()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("payment-succeeded", "{\"valid\": true}");

        int callCount = 0;
        _dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? EventDispatchResult.Retryable("DB timeout")
                    : EventDispatchResult.Success();
            });

        bool commitCalled = false;

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => commitCalled = true);

        // Assert
        Assert.True(result);
        Assert.True(commitCalled);
        Assert.Equal(2, callCount);
        _deadLetterPublisherMock.Verify(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenRetriesExhausted_PublishesToDlqAndCommitsOriginalOffset()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("payment-succeeded", "{\"valid\": false}", offset: 88, partition: 1);

        _dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.Retryable("Persistent timeout", "TimeoutException"));

        DeadLetterMessage? publishedDlq = null;
        _deadLetterPublisherMock
            .Setup(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .Callback<DeadLetterMessage, CancellationToken>((dl, _) => publishedDlq = dl)
            .Returns(Task.CompletedTask);

        bool commitCalled = false;

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => commitCalled = true);

        // Assert
        Assert.True(result);
        Assert.True(commitCalled); // Offset is committed to unblock the partition
        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

        Assert.NotNull(publishedDlq);
        Assert.Equal("payment-succeeded", publishedDlq.OriginalTopic);
        Assert.Equal(1, publishedDlq.OriginalPartition);
        Assert.Equal(88, publishedDlq.OriginalOffset);
        Assert.Equal(3, publishedDlq.AttemptCount);
        Assert.Equal("Persistent timeout", publishedDlq.FailureReason);
        Assert.Equal("TimeoutException", publishedDlq.ExceptionType);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenNonRetryableError_ImmediatelyPublishesToDlqWithoutRetrying()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("payment-succeeded", "{ bad json", offset: 99, partition: 0);

        _dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.NonRetryable("Malformed JSON", "JsonException"));

        DeadLetterMessage? publishedDlq = null;
        _deadLetterPublisherMock
            .Setup(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .Callback<DeadLetterMessage, CancellationToken>((dl, _) => publishedDlq = dl)
            .Returns(Task.CompletedTask);

        bool commitCalled = false;

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => commitCalled = true);

        // Assert
        Assert.True(result);
        Assert.True(commitCalled);
        // Only 1 attempt: retries are bypassed for non-retryable errors
        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(publishedDlq);
        Assert.Equal(1, publishedDlq.AttemptCount);
        Assert.Equal("Malformed JSON", publishedDlq.FailureReason);
        Assert.Equal("JsonException", publishedDlq.ExceptionType);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenDlqPublishFails_DoesNotCommitOffset()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("payment-succeeded", "{ bad json }");

        _dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.NonRetryable("Malformed JSON"));

        _deadLetterPublisherMock
            .Setup(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KafkaException(new Error(ErrorCode.BrokerNotAvailable, "Kafka down")));

        bool commitCalled = false;

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => commitCalled = true);

        // Assert: When DLQ publish fails, offset MUST NOT be committed
        Assert.False(result);
        Assert.False(commitCalled);
    }
}
