using EventPulse.Contracts.Kafka;

namespace EventPulse.BookingService.Events;

public interface IPaymentFailedEventHandler
{
    Task HandleAsync(PaymentFailedEvent paymentEvent, CancellationToken cancellationToken = default);
}
