using EventPulse.BookingService.Events;
using EventPulse.Contracts.Kafka;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

public class LoggingPaymentFailedEventHandler : IPaymentFailedEventHandler
{
    private readonly ILogger<LoggingPaymentFailedEventHandler> _logger;

    public LoggingPaymentFailedEventHandler(ILogger<LoggingPaymentFailedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(PaymentFailedEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Successfully handled PaymentFailedEvent {EventId} for Booking {BookingId}, Payment {PaymentId}, Code: {FailureCode}, Reason: {FailureReason}",
            paymentEvent.EventId,
            paymentEvent.BookingId,
            paymentEvent.PaymentId,
            paymentEvent.FailureCode ?? "N/A",
            paymentEvent.FailureReason ?? "Unknown");

        return Task.CompletedTask;
    }
}
