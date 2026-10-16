using EventPulse.Contracts.Kafka;

namespace EventPulse.EventService.Consumers;

/// <summary>
/// Handler boundary for administrator event submission notifications (EP-149).
/// Encapsulates notification generation and will be implemented by EP-150 for notification persistence.
/// </summary>
public interface IEventSubmittedNotificationHandler
{
    Task HandleAsync(EventSubmittedEvent evt, CancellationToken cancellationToken = default);
}
