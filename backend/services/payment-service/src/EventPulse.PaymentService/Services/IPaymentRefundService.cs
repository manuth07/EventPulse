using EventPulse.Contracts.Kafka.Events;
using EventPulse.PaymentService.Models;

namespace EventPulse.PaymentService.Services;

public interface IPaymentRefundService
{
    Task<RefundRecord?> ProcessRefundAsync(BookingRefundRequestedEvent evt, CancellationToken ct = default);
}
