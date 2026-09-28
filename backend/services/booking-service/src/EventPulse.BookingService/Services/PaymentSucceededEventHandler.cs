using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;
using EventPulse.Contracts.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

public class PaymentSucceededEventHandler : IPaymentSucceededEventHandler
{
    private readonly BookingDbContext _dbContext;
    private readonly IBookingConfirmationService _bookingConfirmationService;
    private readonly ITicketGenerationService _ticketGenerationService;
    private readonly ILogger<PaymentSucceededEventHandler> _logger;

    public PaymentSucceededEventHandler(
        BookingDbContext dbContext,
        IBookingConfirmationService bookingConfirmationService,
        ITicketGenerationService ticketGenerationService,
        ILogger<PaymentSucceededEventHandler> logger)
    {
        _dbContext = dbContext;
        _bookingConfirmationService = bookingConfirmationService;
        _ticketGenerationService = ticketGenerationService;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentSucceededEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing PaymentSucceededEvent {EventId} for Booking {BookingId}, Payment {PaymentId}",
            paymentEvent.EventId, paymentEvent.BookingId, paymentEvent.PaymentId);

        // 1. Check idempotency via ProcessedIntegrationEvents (Inbox)
        var isAlreadyProcessed = await _dbContext.ProcessedIntegrationEvents
            .AnyAsync(e => e.EventId == paymentEvent.EventId, cancellationToken);

        if (isAlreadyProcessed)
        {
            _logger.LogInformation(
                "PaymentSucceededEvent {EventId} for Booking {BookingId} has already been processed. Skipping duplicate.",
                paymentEvent.EventId, paymentEvent.BookingId);
            return;
        }

        // 2. Confirm booking after payment
        var confirmationResult = await _bookingConfirmationService.ConfirmBookingAfterPaymentAsync(
            paymentEvent.BookingId, cancellationToken);

        if (confirmationResult.Status == BookingConfirmationStatus.Confirmed)
        {
            _logger.LogInformation(
                "Booking {BookingId} confirmed from PaymentSucceededEvent {EventId}",
                paymentEvent.BookingId, paymentEvent.EventId);
        }
        else if (confirmationResult.Status == BookingConfirmationStatus.AlreadyConfirmed)
        {
            _logger.LogInformation(
                "Booking {BookingId} already confirmed; ensuring tickets exist for PaymentSucceededEvent {EventId}",
                paymentEvent.BookingId, paymentEvent.EventId);
        }
        else
        {
            _logger.LogError(
                "Failed to confirm Booking {BookingId} for PaymentSucceededEvent {EventId}. Status: {Status}, Message: {Message}",
                paymentEvent.BookingId, paymentEvent.EventId, confirmationResult.Status, confirmationResult.Message);

            throw new InvalidOperationException(
                $"Cannot process PaymentSucceededEvent {paymentEvent.EventId}: Booking {paymentEvent.BookingId} confirmation returned status '{confirmationResult.Status}'. {confirmationResult.Message}");
        }

        // 3. Both Confirmed and AlreadyConfirmed proceed to idempotent ticket generation
        var ticketResult = await _ticketGenerationService.GenerateTicketsForBookingAsync(
            paymentEvent.BookingId, cancellationToken);

        if (!ticketResult.IsSuccess)
        {
            _logger.LogError(
                "Ticket generation failed for Booking {BookingId} (PaymentSucceededEvent {EventId}). Status: {Status}, Message: {Message}",
                paymentEvent.BookingId, paymentEvent.EventId, ticketResult.Status, ticketResult.Message);

            throw new InvalidOperationException(
                $"Ticket generation failed for Booking {paymentEvent.BookingId} with status '{ticketResult.Status}': {ticketResult.Message}");
        }

        _logger.LogInformation(
            "Generated/verified {GeneratedCount} new ticket(s) (Total: {TotalCount}) for Booking {BookingId}",
            ticketResult.GeneratedCount, ticketResult.TotalTicketCount, paymentEvent.BookingId);

        // 4. Record event in Inbox to prevent future duplicate processing
        try
        {
            _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
            {
                EventId = paymentEvent.EventId,
                EventType = nameof(PaymentSucceededEvent),
                Topic = KafkaTopics.PaymentSucceeded,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Recorded ProcessedIntegrationEvent {EventId} for Booking {BookingId}",
                paymentEvent.EventId, paymentEvent.BookingId);
        }
        catch (DbUpdateException ex)
        {
            // Handles any race condition where duplicate event was inserted concurrently
            _logger.LogWarning(ex,
                "ProcessedIntegrationEvent {EventId} was concurrently recorded. Skipping duplicate record.",
                paymentEvent.EventId);
        }
    }
}
