using EventPulse.Contracts.Kafka;
using EventPulse.Contracts.Kafka.Events;
using EventPulse.PaymentService.Models;

namespace EventPulse.PaymentService.Services;

public interface IOutboxWriter
{
    OutboxMessage EnqueuePaymentSucceeded(PaymentSucceededEvent evt);
    OutboxMessage EnqueuePaymentFailed(PaymentFailedEvent evt);
    OutboxMessage EnqueuePaymentRefunded(PaymentRefundedEvent evt);
    OutboxMessage Enqueue<T>(T integrationEvent, string topic, string messageKey, Guid eventId) where T : class;
}
