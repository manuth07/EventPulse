using System.Text.Json;
using EventPulse.Contracts.Kafka;
using EventPulse.PaymentService.Events;

namespace EventPulse.PaymentService.Services;

public class LoggingPaymentEventPublisher : IPaymentEventPublisher
{
    private readonly ILogger<LoggingPaymentEventPublisher> _logger;

    public LoggingPaymentEventPublisher(ILogger<LoggingPaymentEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishPaymentSucceededAsync(PaymentSucceededEvent evt, CancellationToken ct = default)
    {
        _logger.LogInformation("Publishing PaymentSucceededEvent for Payment {PaymentId}, Booking {BookingId}. Payload: {Payload}",
            evt.PaymentId, evt.BookingId, JsonSerializer.Serialize(evt));

        return Task.CompletedTask;
    }

    public Task PublishPaymentFailedAsync(PaymentFailedEvent evt, CancellationToken ct = default)
    {
        _logger.LogInformation("Publishing PaymentFailedEvent for Payment {PaymentId}, Booking {BookingId}. Payload: {Payload}",
            evt.PaymentId, evt.BookingId, JsonSerializer.Serialize(evt));

        return Task.CompletedTask;
    }

    public Task PublishPaymentRefundedAsync(EventPulse.Contracts.Kafka.Events.PaymentRefundedEvent evt, CancellationToken ct = default)
    {
        _logger.LogInformation("Publishing PaymentRefundedEvent for Refund {RefundId}, Payment {PaymentId}, Booking {BookingId}. Payload: {Payload}",
            evt.RefundId, evt.PaymentId, evt.BookingId, JsonSerializer.Serialize(evt));

        return Task.CompletedTask;
    }
}
