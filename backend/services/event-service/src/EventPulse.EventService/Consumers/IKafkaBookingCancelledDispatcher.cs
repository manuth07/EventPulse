namespace EventPulse.EventService.Consumers;

/// <summary>
/// Dispatches incoming Kafka messages from the booking-cancelled topic to restore ticket availability (EP-364).
/// </summary>
public interface IKafkaBookingCancelledDispatcher
{
    Task<EventDispatchResult> DispatchAsync(string topic, string? key, string? value, CancellationToken cancellationToken = default);
}
