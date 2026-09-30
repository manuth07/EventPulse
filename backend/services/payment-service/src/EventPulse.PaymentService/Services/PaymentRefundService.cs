using EventPulse.Contracts.Kafka.Events;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.Events;
using EventPulse.PaymentService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.PaymentService.Services;

public class PaymentRefundService : IPaymentRefundService
{
    private readonly PaymentDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IPaymentEventPublisher? _eventPublisher;
    private readonly ILogger<PaymentRefundService> _logger;

    public PaymentRefundService(
        PaymentDbContext dbContext,
        IOutboxWriter outboxWriter,
        ILogger<PaymentRefundService> logger,
        IPaymentEventPublisher? eventPublisher = null)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task<RefundRecord?> ProcessRefundAsync(BookingRefundRequestedEvent evt, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing refund request {RefundRequestId} for Booking {BookingId}, Amount: {Amount}",
            evt.RefundRequestId, evt.BookingId, evt.RefundAmount);

        // 1. Query payment by BookingId including any existing RefundRecords
        var payment = await _dbContext.Payments
            .Include(p => p.RefundRecords)
            .FirstOrDefaultAsync(p => p.BookingId == evt.BookingId, ct);

        if (payment == null)
        {
            _logger.LogWarning("Payment not found for Booking {BookingId}. Cannot process refund.", evt.BookingId);
            return null;
        }

        // Idempotency Check 1: Check if this specific refund request was already processed
        if (evt.RefundRequestId != Guid.Empty)
        {
            var existingRecord = payment.RefundRecords.FirstOrDefault(r => r.Id == evt.RefundRequestId);
            if (existingRecord != null)
            {
                _logger.LogInformation("Refund request {RefundRequestId} for Booking {BookingId} was already processed. Handling idempotently.",
                    evt.RefundRequestId, evt.BookingId);
                return existingRecord;
            }
        }

        // Idempotency Check 2: If the payment is already Refunded (fully refunded)
        if (payment.Status == PaymentStatus.Refunded)
        {
            _logger.LogInformation("Payment {PaymentId} for Booking {BookingId} is already fully refunded. Handling idempotently.",
                payment.Id, evt.BookingId);
            return payment.RefundRecords.FirstOrDefault();
        }

        // 2. Validate payment is in a refundable state (Succeeded or PartiallyRefunded)
        if (payment.Status != PaymentStatus.Succeeded && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            _logger.LogWarning("Payment {PaymentId} for Booking {BookingId} is in status {Status}, not refundable.",
                payment.Id, evt.BookingId, payment.Status);
            return null;
        }

        // 3. Create a RefundRecord
        var refundId = evt.RefundRequestId != Guid.Empty ? evt.RefundRequestId : Guid.NewGuid();
        var refundRecord = new RefundRecord
        {
            Id = refundId,
            PaymentId = payment.Id,
            BookingId = payment.BookingId,
            Amount = evt.RefundAmount,
            Reason = evt.Reason ?? "Customer requested refund",
            CreatedAt = DateTimeOffset.UtcNow,
            Payment = payment
        };

        _dbContext.RefundRecords.Add(refundRecord);

        // 4. Update Payment Status based on total refunded amount
        var previouslyRefunded = payment.RefundRecords.Sum(r => r.Amount);
        var totalRefunded = previouslyRefunded + evt.RefundAmount;

        if (totalRefunded >= payment.Amount)
        {
            payment.Status = PaymentStatus.Refunded;
        }
        else
        {
            payment.Status = PaymentStatus.PartiallyRefunded;
        }

        // 5. Publish PaymentRefundedEvent
        var refundedEvent = new PaymentRefundedEvent(
            RefundId: refundRecord.Id,
            PaymentId: payment.Id,
            BookingId: payment.BookingId,
            Amount: refundRecord.Amount,
            Status: payment.Status.ToString(),
            RefundedAt: refundRecord.CreatedAt
        );

        _outboxWriter.EnqueuePaymentRefunded(refundedEvent);

        // 6. Save changes in PaymentDbContext
        await _dbContext.SaveChangesAsync(ct);

        if (_eventPublisher != null)
        {
            try
            {
                await _eventPublisher.PublishPaymentRefundedAsync(refundedEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish PaymentRefundedEvent directly; outbox message will handle delivery.");
            }
        }

        _logger.LogInformation("Refund {RefundId} processed successfully for Payment {PaymentId}, Booking {BookingId}. New status: {Status}",
            refundRecord.Id, payment.Id, payment.BookingId, payment.Status);

        return refundRecord;
    }
}
