namespace EventPulse.BookingService.Consumers;

public interface IKafkaPaymentEventDispatcher
{
    Task<bool> DispatchAsync(string topic, string? key, string? value, CancellationToken cancellationToken = default);
}
