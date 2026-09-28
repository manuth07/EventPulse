using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.BookingService.Consumers;

public class KafkaPaymentEventConsumer : BackgroundService
{
    private readonly KafkaOptions _kafkaOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KafkaPaymentEventConsumer> _logger;

    public KafkaPaymentEventConsumer(
        IOptions<KafkaOptions> kafkaOptions,
        IServiceScopeFactory scopeFactory,
        ILogger<KafkaPaymentEventConsumer> logger)
    {
        _kafkaOptions = kafkaOptions.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield to let the host finish startup before blocking on Kafka polling
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = _kafkaOptions.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        var topics = new List<string>
        {
            _kafkaOptions.Topics.PaymentSucceeded,
            _kafkaOptions.Topics.PaymentFailed
        };

        _logger.LogInformation(
            "Initializing Kafka consumer for group '{GroupId}' on '{BootstrapServers}' subscribing to topics: {Topics}",
            config.GroupId, config.BootstrapServers, string.Join(", ", topics));

        using var consumer = new ConsumerBuilder<string, string>(config)
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

        consumer.Subscribe(topics);

        try
        {
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

                    bool processedSuccessfully;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var dispatcher = scope.ServiceProvider.GetRequiredService<IKafkaPaymentEventDispatcher>();
                        processedSuccessfully = await dispatcher.DispatchAsync(
                            consumeResult.Topic,
                            consumeResult.Message.Key,
                            consumeResult.Message.Value,
                            stoppingToken);
                    }

                    if (processedSuccessfully)
                    {
                        consumer.Commit(consumeResult);
                        _logger.LogDebug(
                            "Committed offset {Offset} for partition {Partition} on {Topic}",
                            consumeResult.Offset.Value, consumeResult.Partition.Value, consumeResult.Topic);
                    }
                    else
                    {
                        _logger.LogError(
                            "Processing failed for message on {Topic} partition {Partition} offset {Offset}; offset was NOT committed",
                            consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value);

                        // Back off briefly to prevent a tight CPU loop on persistent errors
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka ConsumeException on topic {Topic}: {Reason}",
                        ex.ConsumerRecord?.Topic ?? "unknown", ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Unexpected error in Kafka consumer loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka consumer shutdown requested for group '{GroupId}'", config.GroupId);
        }
        finally
        {
            try
            {
                consumer.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing Kafka consumer for group '{GroupId}'", config.GroupId);
            }

            _logger.LogInformation("Kafka consumer closed for group '{GroupId}'", config.GroupId);
        }
    }
}
