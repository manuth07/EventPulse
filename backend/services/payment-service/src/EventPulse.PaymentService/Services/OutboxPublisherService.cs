using EventPulse.Contracts.Kafka;
using EventPulse.PaymentService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.PaymentService.Services;

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKafkaMessageProducer _producer;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IServiceScopeFactory scopeFactory,
        IKafkaMessageProducer producer,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<OutboxPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _producer = producer;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var pollingInterval = TimeSpan.FromSeconds(Math.Max(0.5, _kafkaOptions.Outbox.PollingIntervalSeconds));
        var batchSize = Math.Max(1, _kafkaOptions.Outbox.BatchSize);

        _logger.LogInformation("OutboxPublisherService started. Polling every {Interval}s with batch size {BatchSize}",
            pollingInterval.TotalSeconds, batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedFullBatch = await ProcessOutboxBatchAsync(batchSize, stoppingToken);

                // If a full batch was processed, immediately poll next batch without waiting
                if (!processedFullBatch)
                {
                    await Task.Delay(pollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in OutboxPublisherService loop");
                await Task.Delay(pollingInterval, stoppingToken);
            }
        }

        _logger.LogInformation("OutboxPublisherService stopped.");
    }

    internal async Task<bool> ProcessOutboxBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.PublishedAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return false;
        }

        _logger.LogInformation("Found {Count} unpublished outbox message(s) to process", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                _logger.LogInformation("Publishing OutboxMessage {Id} (EventId: {EventId}) to topic {Topic}",
                    message.Id, message.EventId, message.Topic);

                var deliveryResult = await _producer.ProduceAsync(
                    message.Topic,
                    message.MessageKey,
                    message.Payload,
                    cancellationToken);

                message.PublishedAtUtc = DateTimeOffset.UtcNow;
                message.LastError = null;

                _logger.LogInformation(
                    "OutboxMessage {Id} (EventId: {EventId}) published to {Topic} partition {Partition} offset {Offset}",
                    message.Id, message.EventId, message.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.PublishAttempts++;
                message.LastError = $"{ex.GetType().Name}: {ex.Message}";

                _logger.LogWarning(ex,
                    "Failed to publish OutboxMessage {Id} (EventId: {EventId}) to {Topic} on attempt {Attempt}. Error: {Error}",
                    message.Id, message.EventId, message.Topic, message.PublishAttempts, message.LastError);

                // Save messages processed so far (including this failure) and break out of batch
                // so we don't hammer an unavailable Kafka broker
                await dbContext.SaveChangesAsync(cancellationToken);
                return false;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return messages.Count >= batchSize;
    }
}
