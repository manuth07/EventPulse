using EventPulse.Contracts.Kafka;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Services;

public interface IEventOutboxWriter
{
    OutboxMessage EnqueueEventSubmitted(EventSubmittedEvent evt);
    OutboxMessage Enqueue<T>(T integrationEvent, string topic, string messageKey, Guid eventId) where T : class;
}
