namespace EventPulse.BookingService.Consumers;

public interface IKafkaPaymentEventDispatcher
{
    Task<EventDispatchResult> DispatchAsync(string topic, string? key, string? value, CancellationToken cancellationToken = default);
}
