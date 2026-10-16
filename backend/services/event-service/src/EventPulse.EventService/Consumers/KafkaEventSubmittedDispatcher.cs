using System.Text.Json;
using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Data;
using EventPulse.EventService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.EventService.Consumers;

public class KafkaEventSubmittedDispatcher : IKafkaEventSubmittedDispatcher
{
    private readonly KafkaOptions _kafkaOptions;
    private readonly EventDbContext _dbContext;
    private readonly IEventSubmittedNotificationHandler _notificationHandler;
    private readonly ILogger<KafkaEventSubmittedDispatcher> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KafkaEventSubmittedDispatcher(
        IOptions<KafkaOptions> kafkaOptions,
        EventDbContext dbContext,
        IEventSubmittedNotificationHandler notificationHandler,
        ILogger<KafkaEventSubmittedDispatcher> logger)
    {
        _kafkaOptions = kafkaOptions.Value;
        _dbContext = dbContext;
        _notificationHandler = notificationHandler;
        _logger = logger;
    }

    public async Task<EventDispatchResult> DispatchAsync(string topic, string? key, string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogWarning("Received empty or whitespace payload from topic {Topic} with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable("Empty or whitespace payload");
        }

        var configuredTopic = string.IsNullOrWhiteSpace(_kafkaOptions.Topics.EventSubmitted)
            ? KafkaTopics.EventSubmitted
            : _kafkaOptions.Topics.EventSubmitted;

        if (!string.Equals(topic, configuredTopic, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Received message from unhandled topic {Topic} with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable($"Unhandled topic: {topic}");
        }

        EventSubmittedEvent? eventSubmitted;
        try
        {
            eventSubmitted = JsonSerializer.Deserialize<EventSubmittedEvent>(value, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize EventSubmittedEvent from topic {Topic} with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable($"Malformed JSON payload: {ex.Message}", ex.GetType().Name);
        }

        if (eventSubmitted == null)
        {
            _logger.LogError("Deserialized EventSubmittedEvent was null from topic {Topic} with key {Key}", topic, key);
            return EventDispatchResult.NonRetryable("Deserialized EventSubmittedEvent was null");
        }

        // Schema validation
        if (eventSubmitted.EventMessageId == Guid.Empty || eventSubmitted.EventId == Guid.Empty || eventSubmitted.OrganizerId == Guid.Empty)
        {
            _logger.LogError("EventSubmittedEvent missing mandatory identifiers (MessageId={MessageId}, EventId={EventId}, OrganizerId={OrganizerId})",
                eventSubmitted.EventMessageId, eventSubmitted.EventId, eventSubmitted.OrganizerId);
            return EventDispatchResult.NonRetryable("Missing mandatory identifiers");
        }

        _logger.LogInformation("Processing EventSubmittedEvent {MessageId} for Event {EventId} ('{Title}') from topic {Topic}",
            eventSubmitted.EventMessageId, eventSubmitted.EventId, eventSubmitted.EventTitle, topic);

        // 1. Idempotency check via ProcessedIntegrationEvents (Inbox)
        var isAlreadyProcessed = await _dbContext.ProcessedIntegrationEvents
            .AnyAsync(e => e.EventId == eventSubmitted.EventMessageId, cancellationToken);

        if (isAlreadyProcessed)
        {
            _logger.LogInformation(
                "EventSubmittedEvent {MessageId} for Event {EventId} has already been processed. Skipping duplicate.",
                eventSubmitted.EventMessageId, eventSubmitted.EventId);
            return EventDispatchResult.Success();
        }

        // 2. Invoke notification handler and record into inbox
        try
        {
            await _notificationHandler.HandleAsync(eventSubmitted, cancellationToken);

            _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
            {
                EventId = eventSubmitted.EventMessageId,
                EventType = nameof(EventSubmittedEvent),
                Topic = topic,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully processed and recorded EventSubmittedEvent {MessageId} for Event {EventId}",
                eventSubmitted.EventMessageId, eventSubmitted.EventId);

            return EventDispatchResult.Success();
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogInformation("EventSubmittedEvent {MessageId} was concurrently recorded. Skipping duplicate.",
                eventSubmitted.EventMessageId);
            return EventDispatchResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error processing EventSubmittedEvent {MessageId} for Event {EventId}",
                eventSubmitted.EventMessageId, eventSubmitted.EventId);
            return EventDispatchResult.Retryable(ex.Message, ex.GetType().Name);
        }
    }
}
