using EventPulse.BookingService.Events;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

public class LoggingPaymentSucceededEventHandler : IPaymentSucceededEventHandler
{
    private readonly ILogger<LoggingPaymentSucceededEventHandler> _logger;

    public LoggingPaymentSucceededEventHandler(ILogger<LoggingPaymentSucceededEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(PaymentSucceededEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Successfully handled PaymentSucceededEvent {EventId} for Booking {BookingId}, Payment {PaymentId}, Amount {Amount} {Currency}",
            paymentEvent.EventId,
            paymentEvent.BookingId,
            paymentEvent.PaymentId,
            paymentEvent.Amount,
            paymentEvent.Currency);

        return Task.CompletedTask;
    }
}
