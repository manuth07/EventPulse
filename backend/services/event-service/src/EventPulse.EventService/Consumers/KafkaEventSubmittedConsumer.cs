using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.EventService.Consumers;

public class KafkaEventSubmittedConsumer : BackgroundService
{
    private readonly KafkaOptions _kafkaOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDeadLetterPublisher _deadLetterPublisher;
    private readonly ILogger<KafkaEventSubmittedConsumer> _logger;
    private readonly IConsumer<string, string>? _injectedConsumer;

    public KafkaEventSubmittedConsumer(
        IOptions<KafkaOptions> kafkaOptions,
        IServiceScopeFactory scopeFactory,
        IDeadLetterPublisher deadLetterPublisher,
        ILogger<KafkaEventSubmittedConsumer> logger,
        IConsumer<string, string>? consumer = null)
    {
        _kafkaOptions = kafkaOptions.Value;
        _scopeFactory = scopeFactory;
        _deadLetterPublisher = deadLetterPublisher;
        _logger = logger;
        _injectedConsumer = consumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var topic = string.IsNullOrWhiteSpace(_kafkaOptions.Topics.EventSubmitted)
            ? KafkaTopics.EventSubmitted
            : _kafkaOptions.Topics.EventSubmitted;

        var groupId = string.IsNullOrWhiteSpace(_kafkaOptions.ConsumerGroupId)
            ? "eventpulse-event-service"
            : _kafkaOptions.ConsumerGroupId;

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        _logger.LogInformation("Starting KafkaEventSubmittedConsumer for group '{GroupId}' on topic '{Topic}'", groupId, topic);

        IConsumer<string, string>? consumer = _injectedConsumer;
        bool ownsConsumer = false;

        if (consumer == null)
        {
            try
            {
                consumer = new ConsumerBuilder<string, string>(config)
                    .SetErrorHandler((_, e) =>
                    {
                        if (e.IsFatal)
                        {
                            _logger.LogError("Fatal Kafka consumer error: {Reason} (Code: {Code})", e.Reason, e.Code);
                        }
                        else
                        {
                            _logger.LogWarning("Kafka consumer warning: {Reason} (Code: {Code})", e.Reason, e.Code);
                        }
                    })
                    .Build();
                ownsConsumer = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Kafka consumer. Background service will idle.");
                return;
            }
        }

        try
        {
            consumer.Subscribe(topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (consumeResult == null || consumeResult.IsPartitionEOF)
                    {
                        continue;
                    }

                    _logger.LogInformation(
                        "Received message from {Topic} partition {Partition} offset {Offset} (Key: {Key})",
                        consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value, consumeResult.Message.Key);

                    await ProcessMessageAsync(consumeResult, res => consumer.Commit(res), stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka ConsumeException on topic {Topic}: {Reason}", topic, ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Unexpected error in KafkaEventSubmittedConsumer loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("KafkaEventSubmittedConsumer shutdown requested for group '{GroupId}'", groupId);
        }
        finally
        {
            if (ownsConsumer && consumer != null)
            {
                try
                {
                    consumer.Close();
                    consumer.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while closing Kafka consumer");
                }
            }

            _logger.LogInformation("KafkaEventSubmittedConsumer stopped for topic '{Topic}'", topic);
        }
    }

    public async Task<bool> ProcessMessageAsync(
        ConsumeResult<string, string> consumeResult,
        Action<ConsumeResult<string, string>> commitAction,
        CancellationToken stoppingToken = default)
    {
        var maxAttempts = Math.Max(1, _kafkaOptions.Consumer.MaxProcessingAttempts);
        var baseDelaySeconds = Math.Max(0.01, _kafkaOptions.Consumer.RetryBaseDelaySeconds);

        EventDispatchResult? lastResult = null;
        int attempt = 0;
        bool processedSuccessfully = false;

        while (attempt < maxAttempts && !stoppingToken.IsCancellationRequested)
        {
            attempt++;
            using (var scope = _scopeFactory.CreateScope())
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IKafkaEventSubmittedDispatcher>();
                lastResult = await dispatcher.DispatchAsync(
                    consumeResult.Topic,
                    consumeResult.Message.Key,
                    consumeResult.Message.Value,
                    stoppingToken);
            }

            if (lastResult.IsSuccess)
            {
                processedSuccessfully = true;
                break;
            }

            if (!lastResult.IsRetryable)
            {
                _logger.LogWarning(
                    "Non-retryable failure on {Topic} (partition {Partition}, offset {Offset}) on attempt {Attempt}/{MaxAttempts}: {Reason}",
                    consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value, attempt, maxAttempts, lastResult.FailureReason);
                break;
            }

            _logger.LogWarning(
                "Retryable processing failure on {Topic} (partition {Partition}, offset {Offset}) on attempt {Attempt}/{MaxAttempts}: {Reason}",
                consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value, attempt, maxAttempts, lastResult.FailureReason);

            if (attempt < maxAttempts)
            {
                var delayMs = (int)(baseDelaySeconds * Math.Pow(2, attempt - 1) * 1000);
                await Task.Delay(delayMs, stoppingToken);
            }
        }

        if (processedSuccessfully)
        {
            commitAction(consumeResult);
            _logger.LogDebug(
                "Committed offset {Offset} for partition {Partition} on {Topic}",
                consumeResult.Offset.Value, consumeResult.Partition.Value, consumeResult.Topic);
            return true;
        }

        _logger.LogError(
            "Processing failed for message on {Topic} (partition {Partition}, offset {Offset}) after {Attempt} attempt(s). Routing to DLQ. Reason: {Reason}",
            consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value, attempt, lastResult?.FailureReason);

        var deadLetter = new DeadLetterMessage
        {
            OriginalTopic = consumeResult.Topic,
            OriginalPartition = consumeResult.Partition.Value,
            OriginalOffset = consumeResult.Offset.Value,
            MessageKey = consumeResult.Message.Key,
            Payload = consumeResult.Message.Value ?? string.Empty,
            FailureReason = lastResult?.FailureReason ?? "Processing failed",
            ExceptionType = lastResult?.ExceptionType ?? "UnknownException",
            AttemptCount = attempt,
            FailedAtUtc = DateTimeOffset.UtcNow
        };

        try
        {
            await _deadLetterPublisher.PublishDeadLetterAsync(deadLetter, stoppingToken);

            // Crucial: offset is committed ONLY after successful DLQ publish
            commitAction(consumeResult);
            _logger.LogInformation(
                "Poison/failed message routed to DLQ and offset {Offset} committed for {Topic} partition {Partition}",
                consumeResult.Offset.Value, consumeResult.Topic, consumeResult.Partition.Value);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(
                ex,
                "Failed to publish dead letter message to DLQ for {Topic} partition {Partition} offset {Offset}. Offset NOT committed.",
                consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value);

            await Task.Delay(1000, stoppingToken);
            return false;
        }
    }
}
