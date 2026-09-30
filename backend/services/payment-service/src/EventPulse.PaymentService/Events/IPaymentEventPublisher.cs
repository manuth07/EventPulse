using EventPulse.Contracts.Kafka;
using EventPulse.Contracts.Kafka.Events;

namespace EventPulse.PaymentService.Events;

public interface IPaymentEventPublisher
{
    Task PublishPaymentSucceededAsync(PaymentSucceededEvent evt, CancellationToken ct = default);
    Task PublishPaymentFailedAsync(PaymentFailedEvent evt, CancellationToken ct = default);
    Task PublishPaymentRefundedAsync(PaymentRefundedEvent evt, CancellationToken ct = default);
}
