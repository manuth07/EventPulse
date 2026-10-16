using System.Text.Json;
using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Consumers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventPulse.EventService.Tests;

public class KafkaEventSubmittedConsumerTests
{
    private readonly Mock<IKafkaEventSubmittedDispatcher> _dispatcherMock;
    private readonly Mock<IDeadLetterPublisher> _deadLetterPublisherMock;
    private readonly Mock<ILogger<KafkaEventSubmittedConsumer>> _consumerLoggerMock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _kafkaOptions;

    public KafkaEventSubmittedConsumerTests()
    {
        _dispatcherMock = new Mock<IKafkaEventSubmittedDispatcher>();
        _deadLetterPublisherMock = new Mock<IDeadLetterPublisher>();
        _consumerLoggerMock = new Mock<ILogger<KafkaEventSubmittedConsumer>>();

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
                EventSubmitted = "event-submitted",
                EventSubmittedDlq = "event-submitted-dlq"
            },
            Consumer = new KafkaConsumerOptions
            {
                MaxProcessingAttempts = 3,
                RetryBaseDelaySeconds = 0.01 // Fast retries in test
            }
        };
    }

    private KafkaEventSubmittedConsumer CreateConsumer()
    {
        return new KafkaEventSubmittedConsumer(
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
        var consumeResult = CreateConsumeResult("event-submitted", "key-1", "{\"EventTitle\":\"Test\"}");
        bool committed = false;

        _dispatcherMock
            .Setup(d => d.DispatchAsync("event-submitted", "key-1", consumeResult.Message.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.Success());

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => committed = true);

        // Assert
        Assert.True(result);
        Assert.True(committed);
        _deadLetterPublisherMock.Verify(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenDispatcherReturnsRetryable_RetriesUpToMaxAttemptsAndRoutesToDlq()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("event-submitted", "key-1", "{\"EventTitle\":\"Test\"}", partition: 1, offset: 200);
        bool committed = false;
        DeadLetterMessage? publishedDlq = null;

        _dispatcherMock
            .Setup(d => d.DispatchAsync("event-submitted", "key-1", consumeResult.Message.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.Retryable("DB transient failure", "TimeoutException"));

        _deadLetterPublisherMock
            .Setup(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .Callback<DeadLetterMessage, CancellationToken>((dl, _) => publishedDlq = dl)
            .Returns(Task.CompletedTask);

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => committed = true);

        // Assert
        Assert.True(result);
        Assert.True(committed); // Committed after DLQ routing!

        // Must have retried maxAttempts (3) times
        _dispatcherMock.Verify(d => d.DispatchAsync("event-submitted", "key-1", consumeResult.Message.Value, It.IsAny<CancellationToken>()), Times.Exactly(3));

        // Must have published to DLQ with attempt count and metadata
        Assert.NotNull(publishedDlq);
        Assert.Equal("event-submitted", publishedDlq.OriginalTopic);
        Assert.Equal(1, publishedDlq.OriginalPartition);
        Assert.Equal(200, publishedDlq.OriginalOffset);
        Assert.Equal("key-1", publishedDlq.MessageKey);
        Assert.Equal(3, publishedDlq.AttemptCount);
        Assert.Equal("DB transient failure", publishedDlq.FailureReason);
        Assert.Equal("TimeoutException", publishedDlq.ExceptionType);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenDispatcherReturnsNonRetryable_RoutesDirectlyToDlqWithoutRetrying()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("event-submitted", "key-bad", "{ malformed json");
        bool committed = false;
        DeadLetterMessage? publishedDlq = null;

        _dispatcherMock
            .Setup(d => d.DispatchAsync("event-submitted", "key-bad", consumeResult.Message.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.NonRetryable("Malformed JSON payload: unexpected token", "JsonException"));

        _deadLetterPublisherMock
            .Setup(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .Callback<DeadLetterMessage, CancellationToken>((dl, _) => publishedDlq = dl)
            .Returns(Task.CompletedTask);

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => committed = true);

        // Assert
        Assert.True(result);
        Assert.True(committed);

        // Non-retryable error should only be dispatched ONCE (no useless retries)
        _dispatcherMock.Verify(d => d.DispatchAsync("event-submitted", "key-bad", consumeResult.Message.Value, It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(publishedDlq);
        Assert.Equal(1, publishedDlq.AttemptCount);
        Assert.Contains("Malformed JSON payload", publishedDlq.FailureReason);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenDlqPublishFails_DoesNotCommitOffset()
    {
        // Arrange
        var consumer = CreateConsumer();
        var consumeResult = CreateConsumeResult("event-submitted", "key-1", "bad");
        bool committed = false;

        _dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventDispatchResult.NonRetryable("Poison pill"));

        _deadLetterPublisherMock
            .Setup(d => d.PublishDeadLetterAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka broker down"));

        // Act
        var result = await consumer.ProcessMessageAsync(consumeResult, _ => committed = true);

        // Assert
        Assert.False(result);
        Assert.False(committed); // Must NOT commit offset when DLQ fails!
    }

    [Fact]
    public async Task KafkaDeadLetterPublisher_PublishDeadLetterAsync_PublishesToCorrectTopicAndKey()
    {
        // Arrange
        var producerMock = new Mock<IProducer<string, string>>();
        var loggerMock = new Mock<ILogger<KafkaDeadLetterPublisher>>();

        Message<string, string>? producedMessage = null;
        string? producedTopic = null;

        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((t, m, _) =>
            {
                producedTopic = t;
                producedMessage = m;
            })
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "event-submitted-dlq",
                Partition = new Partition(0),
                Offset = new Offset(1)
            });

        var dlqPublisher = new KafkaDeadLetterPublisher(
            Options.Create(_kafkaOptions),
            loggerMock.Object,
            producerMock.Object);

        var deadLetter = new DeadLetterMessage
        {
            OriginalTopic = "event-submitted",
            OriginalPartition = 0,
            OriginalOffset = 55,
            MessageKey = "event-key-123",
            Payload = "{\"Test\":true}",
            FailureReason = "Handler failed",
            ExceptionType = "InvalidOperationException",
            AttemptCount = 3
        };

        // Act
        await dlqPublisher.PublishDeadLetterAsync(deadLetter);

        // Assert
        Assert.Equal("event-submitted-dlq", producedTopic);
        Assert.NotNull(producedMessage);
        Assert.Equal("event-key-123", producedMessage.Key);

        var deserialized = JsonSerializer.Deserialize<DeadLetterMessage>(producedMessage.Value);
        Assert.NotNull(deserialized);
        Assert.Equal("event-submitted", deserialized.OriginalTopic);
        Assert.Equal(55, deserialized.OriginalOffset);
        Assert.Equal("Handler failed", deserialized.FailureReason);
    }
}
