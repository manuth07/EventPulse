using EventPulse.Contracts.Kafka.Events;

namespace EventPulse.BookingService.Events;

public interface IPaymentRefundedEventHandler
{
    Task HandleAsync(PaymentRefundedEvent refundedEvent, CancellationToken cancellationToken = default);
}
