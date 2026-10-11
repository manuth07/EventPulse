using EventPulse.BookingService.Data;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;
using EventPulse.Contracts.Kafka;
using EventPulse.Contracts.Kafka.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

public class PaymentRefundedEventHandler : IPaymentRefundedEventHandler
{
    private readonly BookingDbContext _dbContext;
    private readonly ISalesReportingService _salesReportingService;
    private readonly ILogger<PaymentRefundedEventHandler> _logger;

    public PaymentRefundedEventHandler(
        BookingDbContext dbContext,
        ISalesReportingService salesReportingService,
        ILogger<PaymentRefundedEventHandler> logger)
    {
        _dbContext = dbContext;
        _salesReportingService = salesReportingService;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentRefundedEvent refundedEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EP-383: Processing PaymentRefundedEvent for RefundId {RefundId}, Booking {BookingId}, Payment {PaymentId}, Amount {Amount}",
            refundedEvent.RefundId, refundedEvent.BookingId, refundedEvent.PaymentId, refundedEvent.Amount);

        // 1. Check idempotency via ProcessedIntegrationEvents (Inbox)
        var isAlreadyProcessed = await _dbContext.ProcessedIntegrationEvents
            .AnyAsync(e => e.EventId == refundedEvent.RefundId, cancellationToken);

        if (isAlreadyProcessed)
        {
            _logger.LogInformation(
                "PaymentRefundedEvent {RefundId} for Booking {BookingId} already processed. Skipping duplicate.",
                refundedEvent.RefundId, refundedEvent.BookingId);
            return;
        }

        // 2. Apply refund adjustment to SalesReport
        await _salesReportingService.ApplyRefundAdjustmentAsync(
            bookingId: refundedEvent.BookingId,
            refundId: refundedEvent.RefundId,
            refundAmount: refundedEvent.Amount,
            refundStatus: refundedEvent.Status,
            reason: "Payment refunded event from PaymentService",
            refundedAtUtc: refundedEvent.RefundedAt,
            cancellationToken: cancellationToken);

        // 3. Record in ProcessedIntegrationEvents (Inbox)
        try
        {
            _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
            {
                EventId = refundedEvent.RefundId,
                EventType = nameof(PaymentRefundedEvent),
                Topic = KafkaTopics.PaymentRefunded,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "ProcessedIntegrationEvent {RefundId} was concurrently recorded. Skipping duplicate record.",
                refundedEvent.RefundId);
        }
    }
}
