namespace EventPulse.EventService.Consumers;

public interface IKafkaEventSubmittedDispatcher
{
    Task<EventDispatchResult> DispatchAsync(string topic, string? key, string? value, CancellationToken cancellationToken = default);
}
