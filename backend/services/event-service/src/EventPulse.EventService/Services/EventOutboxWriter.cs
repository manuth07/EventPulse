using System.Text.Json;
using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Data;
using EventPulse.EventService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.EventService.Services;

public class EventOutboxWriter : IEventOutboxWriter
{
    private readonly EventDbContext _dbContext;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<EventOutboxWriter> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public EventOutboxWriter(
        EventDbContext dbContext,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<EventOutboxWriter> logger)
    {
        _dbContext = dbContext;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
    }

    public OutboxMessage EnqueueEventSubmitted(EventSubmittedEvent evt)
    {
        var topic = string.IsNullOrWhiteSpace(_kafkaOptions.Topics.EventSubmitted)
            ? KafkaTopics.EventSubmitted
            : _kafkaOptions.Topics.EventSubmitted;

        return Enqueue(evt, topic, evt.EventId.ToString(), evt.EventMessageId);
    }

    public OutboxMessage Enqueue<T>(T integrationEvent, string topic, string messageKey, Guid eventId) where T : class
    {
        var payload = JsonSerializer.Serialize(integrationEvent, SerializerOptions);
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = typeof(T).Name,
            Topic = topic,
            MessageKey = messageKey,
            Payload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PublishedAtUtc = null,
            PublishAttempts = 0
        };

        _dbContext.OutboxMessages.Add(message);
        _logger.LogInformation("Enqueued OutboxMessage {Id} for Event {EventId} ({EventType}) to topic {Topic}",
            message.Id, message.EventId, message.EventType, message.Topic);

        return message;
    }
}
