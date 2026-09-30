using System.Text.Json;
using Confluent.Kafka;
using EventPulse.Contracts.Kafka;
using EventPulse.Contracts.Kafka.Events;
using EventPulse.PaymentService.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.PaymentService.Consumers;

public class KafkaBookingRefundConsumer : BackgroundService
{
    private readonly KafkaOptions _kafkaOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KafkaBookingRefundConsumer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KafkaBookingRefundConsumer(
        IOptions<KafkaOptions> kafkaOptions,
        IServiceScopeFactory scopeFactory,
        ILogger<KafkaBookingRefundConsumer> logger)
    {
        _kafkaOptions = kafkaOptions.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield to allow ASP.NET Core host to finish startup before polling Kafka
        await Task.Yield();

        var topic = _kafkaOptions.Topics.BookingRefundRequested;
        var groupId = $"{_kafkaOptions.ConsumerGroupId}-refunds";

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        _logger.LogInformation("Starting KafkaBookingRefundConsumer for group '{GroupId}' on topic '{Topic}'", groupId, topic);

        IConsumer<string, string>? consumer = null;

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

                    _logger.LogInformation("Received BookingRefundRequested message from {Topic} partition {Partition} offset {Offset} (Key: {Key})",
                        consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value, consumeResult.Message.Key);

                    var success = await ProcessMessageAsync(consumeResult.Message.Value, stoppingToken);
                    if (success)
                    {
                        consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka ConsumeException on topic {Topic}: {Reason}", topic, ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Unexpected error in KafkaBookingRefundConsumer loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("KafkaBookingRefundConsumer cancellation requested for group '{GroupId}'", groupId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka connection or initialization failed for KafkaBookingRefundConsumer. Background worker will idle.");
        }
        finally
        {
            if (consumer != null)
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

            _logger.LogInformation("KafkaBookingRefundConsumer stopped for topic '{Topic}'", topic);
        }
    }

    public async Task<bool> ProcessMessageAsync(string messageValue, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(messageValue))
        {
            _logger.LogWarning("Received empty or null refund message");
            return true;
        }

        try
        {
            var refundEvent = JsonSerializer.Deserialize<BookingRefundRequestedEvent>(messageValue, JsonOptions);
            if (refundEvent == null)
            {
                _logger.LogWarning("Failed to deserialize BookingRefundRequestedEvent: {Message}", messageValue);
                return true;
            }

            using var scope = _scopeFactory.CreateScope();
            var refundService = scope.ServiceProvider.GetRequiredService<IPaymentRefundService>();

            await refundService.ProcessRefundAsync(refundEvent, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing BookingRefundRequested message: {Message}", messageValue);
            return false;
        }
    }
}
