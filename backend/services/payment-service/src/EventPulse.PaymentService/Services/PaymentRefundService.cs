using EventPulse.Contracts.Kafka.Events;
using EventPulse.PaymentService.Data;
using EventPulse.PaymentService.Events;
using EventPulse.PaymentService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.PaymentService.Services;

/// <summary>
/// Implements cancellation refund processing, payment status transitions, and Stripe reconciliation (EP-365).
/// </summary>
public class PaymentRefundService : IPaymentRefundService
{
    private readonly PaymentDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IPaymentEventPublisher? _eventPublisher;
    private readonly IStripeRefundService _stripeRefundService;
    private readonly ILogger<PaymentRefundService> _logger;

    public PaymentRefundService(
        PaymentDbContext dbContext,
        IOutboxWriter outboxWriter,
        ILogger<PaymentRefundService> logger,
        IPaymentEventPublisher? eventPublisher = null,
        IStripeRefundService? stripeRefundService = null)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _logger = logger;
        _eventPublisher = eventPublisher;
        _stripeRefundService = stripeRefundService ?? new StripeRefundService(new ConfigurationBuilder().Build(), new LoggerFactory().CreateLogger<StripeRefundService>());
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

        // Idempotency Check 2: If the payment is already fully refunded
        if (payment.Status == PaymentStatus.Refunded)
        {
            _logger.LogInformation("Payment {PaymentId} for Booking {BookingId} is already fully refunded. Handling idempotently.",
                payment.Id, evt.BookingId);
            return payment.RefundRecords.FirstOrDefault();
        }

        // 2. Validate payment is in a refundable state (Succeeded or PartiallyRefunded)
        if (payment.Status != PaymentStatus.Succeeded && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            _logger.LogWarning("Payment {PaymentId} for Booking {BookingId} is in status '{Status}', not eligible for refund.",
                payment.Id, evt.BookingId, payment.Status);

            var notRefundableId = evt.RefundRequestId != Guid.Empty ? evt.RefundRequestId : Guid.NewGuid();
            var notRefundableRecord = new RefundRecord
            {
                Id = notRefundableId,
                PaymentId = payment.Id,
                BookingId = payment.BookingId,
                Amount = 0m,
                Currency = payment.Currency,
                Status = RefundStatus.NotRefundable,
                Reason = $"Payment is in status '{payment.Status}', not eligible for refund.",
                CreatedAt = DateTimeOffset.UtcNow,
                Payment = payment
            };
            _dbContext.RefundRecords.Add(notRefundableRecord);
            await _dbContext.SaveChangesAsync(ct);
            return notRefundableRecord;
        }

        // 3. Evaluate refund amount and financial bounds (Never refund more than eligible)
        if (evt.RefundAmount <= 0)
        {
            _logger.LogInformation("Refund amount for Booking {BookingId} is 0.00 (No Refund outcome per policy).", evt.BookingId);
            var zeroRecord = new RefundRecord
            {
                Id = evt.RefundRequestId != Guid.Empty ? evt.RefundRequestId : Guid.NewGuid(),
                PaymentId = payment.Id,
                BookingId = payment.BookingId,
                Amount = 0m,
                Currency = payment.Currency,
                Status = RefundStatus.NotRefundable,
                Reason = evt.Reason ?? "No refund applicable per cancellation policy",
                CreatedAt = DateTimeOffset.UtcNow,
                Payment = payment
            };
            _dbContext.RefundRecords.Add(zeroRecord);
            await _dbContext.SaveChangesAsync(ct);
            return zeroRecord;
        }

        var previouslyRefunded = payment.RefundRecords
            .Where(r => r.Status == RefundStatus.Completed)
            .Sum(r => r.Amount);
        var remainingEligible = Math.Max(0m, payment.Amount - previouslyRefunded);

        if (remainingEligible <= 0)
        {
            _logger.LogWarning("Payment {PaymentId} has no remaining refundable amount (Eligible: 0.00).", payment.Id);
            payment.Status = PaymentStatus.Refunded;
            await _dbContext.SaveChangesAsync(ct);
            return payment.RefundRecords.FirstOrDefault();
        }

        // Cap refund amount to remaining eligible amount
        var finalRefundAmount = Math.Min(evt.RefundAmount, remainingEligible);

        // 4. Create initial RefundRecord
        var refundId = evt.RefundRequestId != Guid.Empty ? evt.RefundRequestId : Guid.NewGuid();
        var refundRecord = new RefundRecord
        {
            Id = refundId,
            PaymentId = payment.Id,
            BookingId = payment.BookingId,
            Amount = finalRefundAmount,
            Currency = payment.Currency,
            Reason = evt.Reason ?? "Customer requested cancellation refund",
            CreatedAt = DateTimeOffset.UtcNow,
            Payment = payment
        };

        // 5. Stripe Refund Execution with Idempotency Key
        var idempotencyKey = $"refund-{refundId}";
        var paymentIdentifier = payment.StripePaymentIntentId ?? payment.StripeSessionId ?? string.Empty;

        var stripeResult = await _stripeRefundService.CreateRefundAsync(
            paymentIdentifier,
            finalRefundAmount,
            payment.Currency,
            refundRecord.Reason,
            idempotencyKey,
            ct);

        if (!stripeResult.Success)
        {
            _logger.LogError("Stripe refund failed for Payment {PaymentId}: {Error}", payment.Id, stripeResult.FailureReason);
            refundRecord.Status = RefundStatus.Failed;
            refundRecord.FailureReason = stripeResult.FailureReason;
            payment.Status = PaymentStatus.RefundFailed;

            _dbContext.RefundRecords.Add(refundRecord);
            await _dbContext.SaveChangesAsync(ct);
            return refundRecord;
        }

        if (string.Equals(stripeResult.Status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Stripe refund {RefundId} is pending. Awaiting webhook confirmation.", stripeResult.StripeRefundId);
            refundRecord.Status = RefundStatus.Pending;
            refundRecord.StripeRefundId = stripeResult.StripeRefundId;

            // Per specification: Do NOT mark refund completed or transition payment status until actually confirmed
            _dbContext.RefundRecords.Add(refundRecord);
            await _dbContext.SaveChangesAsync(ct);
            return refundRecord;
        }

        // 6. Mark Refund Completed
        refundRecord.Status = RefundStatus.Completed;
        refundRecord.CompletedAt = DateTimeOffset.UtcNow;
        refundRecord.StripeRefundId = stripeResult.StripeRefundId;
        _dbContext.RefundRecords.Add(refundRecord);

        // 7. Update Payment Status based on total completed refunded amount
        var totalRefunded = previouslyRefunded + finalRefundAmount;
        if (totalRefunded >= payment.Amount)
        {
            payment.Status = PaymentStatus.Refunded;
        }
        else
        {
            payment.Status = PaymentStatus.PartiallyRefunded;
        }

        // 8. Publish PaymentRefundedEvent
        var refundedEvent = new PaymentRefundedEvent(
            RefundId: refundRecord.Id,
            PaymentId: payment.Id,
            BookingId: payment.BookingId,
            Amount: refundRecord.Amount,
            Status: payment.Status.ToString(),
            RefundedAt: refundRecord.CompletedAt ?? refundRecord.CreatedAt
        );

        _outboxWriter.EnqueuePaymentRefunded(refundedEvent);

        // 9. Save changes atomically within Payment Service
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

        _logger.LogInformation(
            "Refund {RefundId} processed successfully for Payment {PaymentId}, Booking {BookingId}. Amount: {Amount} {Currency}, Status: {PaymentStatus}, RefundStatus: {RefundStatus}",
            refundRecord.Id, payment.Id, payment.BookingId, refundRecord.Amount, refundRecord.Currency, payment.Status, refundRecord.Status);

        return refundRecord;
    }
}
