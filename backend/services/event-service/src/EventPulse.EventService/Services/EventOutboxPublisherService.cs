using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.EventService.Services;

public class EventOutboxPublisherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKafkaMessageProducer _producer;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<EventOutboxPublisherService> _logger;
    private DateTimeOffset _lastCleanupUtc = DateTimeOffset.MinValue;

    public EventOutboxPublisherService(
        IServiceScopeFactory scopeFactory,
        IKafkaMessageProducer producer,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<EventOutboxPublisherService> logger)
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
        var cleanupInterval = TimeSpan.FromMinutes(Math.Max(1, _kafkaOptions.Outbox.CleanupIntervalMinutes));
        var retentionDays = Math.Max(1, _kafkaOptions.Outbox.RetentionDays);

        _logger.LogInformation(
            "EventOutboxPublisherService started. Polling every {Interval}s with batch size {BatchSize}. Retention: {RetentionDays} days (cleanup every {CleanupMinutes}m).",
            pollingInterval.TotalSeconds, batchSize, retentionDays, cleanupInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Process unpublished outbox batch
                var processedFullBatch = await ProcessOutboxBatchAsync(batchSize, stoppingToken);

                // 2. Low-frequency retention cleanup for expired published rows
                if (DateTimeOffset.UtcNow - _lastCleanupUtc >= cleanupInterval)
                {
                    await CleanupExpiredPublishedMessagesAsync(stoppingToken);
                    _lastCleanupUtc = DateTimeOffset.UtcNow;
                }

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
                _logger.LogError(ex, "Unhandled exception in EventOutboxPublisherService loop");
                await Task.Delay(pollingInterval, stoppingToken);
            }
        }

        _logger.LogInformation("EventOutboxPublisherService stopped.");
    }

    public async Task<bool> ProcessOutboxBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.PublishedAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            _logger.LogTrace("No unpublished outbox messages found.");
            return false;
        }

        _logger.LogInformation("Found {Count} unpublished outbox message(s) to process", messages.Count);

        int publishedCount = 0;
        foreach (var message in messages)
        {
            try
            {
                var deliveryResult = await _producer.ProduceAsync(
                    message.Topic,
                    message.MessageKey,
                    message.Payload,
                    cancellationToken);

                message.PublishedAtUtc = DateTimeOffset.UtcNow;
                message.LastError = null;
                publishedCount++;

                _logger.LogInformation(
                    "Published outbox event {EventId} to {Topic} (Partition: {Partition}, Offset: {Offset})",
                    message.EventId, message.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.PublishAttempts++;
                message.LastError = $"{ex.GetType().Name}: {ex.Message}";

                _logger.LogWarning(ex,
                    "Failed to publish OutboxMessage {Id} (EventId: {EventId}) to {Topic} on attempt {Attempt}. Error: {Error}",
                    message.Id, message.EventId, message.Topic, message.PublishAttempts, message.LastError);

                // Save messages processed so far (including this failure) and break out of batch
                await dbContext.SaveChangesAsync(cancellationToken);
                return false;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Published {Count} outbox events in batch", publishedCount);
        return messages.Count >= batchSize;
    }

    public async Task<int> CleanupExpiredPublishedMessagesAsync(CancellationToken cancellationToken = default)
    {
        var retentionDays = Math.Max(1, _kafkaOptions.Outbox.RetentionDays);
        var cutoffUtc = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();

            var expiredMessages = await dbContext.OutboxMessages
                .Where(m => m.PublishedAtUtc != null && m.PublishedAtUtc <= cutoffUtc)
                .OrderBy(m => m.PublishedAtUtc)
                .Take(500)
                .ToListAsync(cancellationToken);

            if (expiredMessages.Count == 0)
            {
                _logger.LogTrace("Outbox retention cleanup found no expired published messages older than {Days} days.", retentionDays);
                return 0;
            }

            dbContext.OutboxMessages.RemoveRange(expiredMessages);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Removed {Count} published outbox message(s) older than {Days} days.",
                expiredMessages.Count, retentionDays);

            return expiredMessages.Count;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up expired published outbox messages.");
            return 0;
        }
    }
}
