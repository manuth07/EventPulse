using EventPulse.Contracts.Kafka;

namespace EventPulse.BookingService.Events;

public interface IPaymentSucceededEventHandler
{
    Task HandleAsync(PaymentSucceededEvent paymentEvent, CancellationToken cancellationToken = default);
}
