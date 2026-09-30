using System.Text.Json;
using EventPulse.Contracts.Kafka;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPulse.PaymentService.Services;

public class OutboxWriter : IOutboxWriter
{
    private readonly PaymentDbContext _dbContext;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<OutboxWriter> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public OutboxWriter(
        PaymentDbContext dbContext,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<OutboxWriter> logger)
    {
        _dbContext = dbContext;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
    }

    public OutboxMessage EnqueuePaymentSucceeded(PaymentSucceededEvent evt)
    {
        return Enqueue(evt, _kafkaOptions.Topics.PaymentSucceeded, evt.BookingId.ToString(), evt.EventId);
    }

    public OutboxMessage EnqueuePaymentFailed(PaymentFailedEvent evt)
    {
        return Enqueue(evt, _kafkaOptions.Topics.PaymentFailed, evt.BookingId.ToString(), evt.EventId);
    }

    public OutboxMessage EnqueuePaymentRefunded(EventPulse.Contracts.Kafka.Events.PaymentRefundedEvent evt)
    {
        return Enqueue(evt, _kafkaOptions.Topics.PaymentRefunded, evt.BookingId.ToString(), evt.RefundId);
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
