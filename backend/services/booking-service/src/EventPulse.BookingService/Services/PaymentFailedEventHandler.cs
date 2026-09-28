using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;
using EventPulse.Contracts.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

public class PaymentFailedEventHandler : IPaymentFailedEventHandler
{
    private readonly BookingDbContext _dbContext;
    private readonly IBookingPaymentFailureService _failureService;
    private readonly ILogger<PaymentFailedEventHandler> _logger;

    public PaymentFailedEventHandler(
        BookingDbContext dbContext,
        IBookingPaymentFailureService failureService,
        ILogger<PaymentFailedEventHandler> logger)
    {
        _dbContext = dbContext;
        _failureService = failureService;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentFailedEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing PaymentFailedEvent {EventId} for Booking {BookingId}, Payment {PaymentId}, Code: {FailureCode}, Reason: {FailureReason}",
            paymentEvent.EventId,
            paymentEvent.BookingId,
            paymentEvent.PaymentId,
            paymentEvent.FailureCode ?? "N/A",
            paymentEvent.FailureReason ?? "Unknown");

        // 1. Check idempotency via ProcessedIntegrationEvents (Inbox)
        var isAlreadyProcessed = await _dbContext.ProcessedIntegrationEvents
            .AnyAsync(e => e.EventId == paymentEvent.EventId, cancellationToken);

        if (isAlreadyProcessed)
        {
            _logger.LogInformation(
                "PaymentFailedEvent {EventId} for Booking {BookingId} has already been processed. Skipping duplicate.",
                paymentEvent.EventId, paymentEvent.BookingId);
            return;
        }

        // 2. Invoke booking payment-failure transition
        var result = await _failureService.HandlePaymentFailureAsync(
            paymentEvent.BookingId,
            paymentEvent.FailureCode,
            paymentEvent.FailureReason,
            cancellationToken);

        switch (result.Status)
        {
            case BookingPaymentFailureStatus.Failed:
                _logger.LogInformation(
                    "Booking {BookingId} transitioned to PaymentFailed for PaymentFailedEvent {EventId}",
                    paymentEvent.BookingId, paymentEvent.EventId);
                break;

            case BookingPaymentFailureStatus.AlreadyFailed:
                _logger.LogInformation(
                    "Booking {BookingId} was already in PaymentFailed status; treating operation as idempotent for Event {EventId}",
                    paymentEvent.BookingId, paymentEvent.EventId);
                break;

            case BookingPaymentFailureStatus.AlreadyConfirmed:
                _logger.LogWarning(
                    "Late or stale PaymentFailedEvent {EventId} received for Booking {BookingId}, but booking is already Confirmed. Preserving Confirmed status.",
                    paymentEvent.EventId, paymentEvent.BookingId);
                break;

            case BookingPaymentFailureStatus.Cancelled:
                _logger.LogInformation(
                    "PaymentFailedEvent {EventId} received for Booking {BookingId}, but booking is already Cancelled. Preserving Cancelled status.",
                    paymentEvent.EventId, paymentEvent.BookingId);
                break;

            case BookingPaymentFailureStatus.NotFound:
                _logger.LogError(
                    "Booking {BookingId} was not found when processing PaymentFailedEvent {EventId}",
                    paymentEvent.BookingId, paymentEvent.EventId);
                throw new InvalidOperationException($"Booking {paymentEvent.BookingId} not found for PaymentFailedEvent {paymentEvent.EventId}");

            default:
                _logger.LogWarning(
                    "Unhandled failure status {Status} for Booking {BookingId}, Event {EventId}",
                    result.Status, paymentEvent.BookingId, paymentEvent.EventId);
                break;
        }

        // 3. Record event in Inbox to prevent future duplicate processing
        try
        {
            _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
            {
                EventId = paymentEvent.EventId,
                EventType = nameof(PaymentFailedEvent),
                Topic = KafkaTopics.PaymentFailed,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Recorded ProcessedIntegrationEvent {EventId} for Booking {BookingId}",
                paymentEvent.EventId, paymentEvent.BookingId);
        }
        catch (DbUpdateException ex)
        {
            // Handles concurrent duplicate deliveries safely
            _logger.LogWarning(ex,
                "ProcessedIntegrationEvent {EventId} was concurrently recorded. Skipping duplicate record.",
                paymentEvent.EventId);
        }
    }
}
