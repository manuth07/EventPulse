using EventPulse.BookingService.Data;
using EventPulse.BookingService.Models;
using EventPulse.Contracts.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPulse.BookingService.Services;

public class SalesReportingService : ISalesReportingService
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventAvailabilityClient _eventAvailabilityClient;
    private readonly ILogger<SalesReportingService> _logger;

    public SalesReportingService(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventAvailabilityClient,
        ILogger<SalesReportingService> logger)
    {
        _dbContext = dbContext;
        _eventAvailabilityClient = eventAvailabilityClient;
        _logger = logger;
    }

    public async Task<SalesReport> GenerateSalesReportOnPurchaseAsync(
        PaymentSucceededEvent paymentEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EP-383: Processing sales report creation for PaymentSucceededEvent {EventId}, Booking {BookingId}, Payment {PaymentId}",
            paymentEvent.EventId, paymentEvent.BookingId, paymentEvent.PaymentId);

        // 1. Idempotency Check by PaymentId
        var existingByPayment = await _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .FirstOrDefaultAsync(s => s.PaymentId == paymentEvent.PaymentId, cancellationToken);

        if (existingByPayment != null)
        {
            _logger.LogInformation(
                "SalesReport for PaymentId {PaymentId} already exists (SaleId: {SaleId}). Idempotently skipping duplicate.",
                paymentEvent.PaymentId, existingByPayment.Id);
            return existingByPayment;
        }

        // 2. Idempotency Check by BookingId (at most one confirmed sale per booking)
        var existingByBooking = await _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .FirstOrDefaultAsync(s => s.BookingId == paymentEvent.BookingId, cancellationToken);

        if (existingByBooking != null)
        {
            _logger.LogInformation(
                "SalesReport for BookingId {BookingId} already exists (SaleId: {SaleId}). Multiple payment attempts prevented duplicate sale creation.",
                paymentEvent.BookingId, existingByBooking.Id);
            return existingByBooking;
        }

        // 3. Load authoritative Booking and line items
        var booking = await _dbContext.Bookings
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == paymentEvent.BookingId, cancellationToken);

        if (booking == null)
        {
            _logger.LogError("Cannot generate sales report: Booking {BookingId} not found", paymentEvent.BookingId);
            throw new InvalidOperationException($"Booking {paymentEvent.BookingId} not found in BookingService");
        }

        // 4. Resolve OrganizerId from EventService
        Guid organizerId = Guid.Empty;
        try
        {
            var eventSummary = await _eventAvailabilityClient.GetEventSummaryAsync(booking.EventId, cancellationToken);
            if (eventSummary != null && eventSummary.OrganizerId != Guid.Empty)
            {
                organizerId = eventSummary.OrganizerId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve OrganizerId for Event {EventId} from EventService. Proceeding with recorded details.", booking.EventId);
        }

        // 5. Construct SalesReport and preserve line items
        var saleAmount = paymentEvent.Amount > 0 ? paymentEvent.Amount : booking.TotalAmount;
        var currency = !string.IsNullOrWhiteSpace(paymentEvent.Currency) ? paymentEvent.Currency : "USD";
        var paymentTimestamp = paymentEvent.OccurredAtUtc != default ? paymentEvent.OccurredAtUtc : DateTimeOffset.UtcNow;

        var salesReport = new SalesReport
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentEvent.PaymentId,
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            EventId = booking.EventId,
            OrganizerId = organizerId,
            SaleAmount = saleAmount,
            Currency = currency,
            PaymentTimestampUtc = paymentTimestamp,
            RefundedAmount = 0m,
            RefundStatus = SalesRefundStatus.None,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        foreach (var item in booking.Items)
        {
            salesReport.Items.Add(new SalesReportItem
            {
                Id = Guid.NewGuid(),
                SalesReportId = salesReport.Id,
                BookingItemId = item.Id,
                TicketTypeId = item.TicketTypeId,
                TicketTypeName = item.TicketName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Subtotal,
                RefundedQuantity = 0,
                RefundedAmount = 0m
            });
        }

        // 6. Save changes with concurrency/duplicate protection
        try
        {
            _dbContext.SalesReports.Add(salesReport);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully generated SalesReport {SaleId} for Booking {BookingId}, Payment {PaymentId}, Amount {Amount} {Currency} with {LineItemCount} item(s)",
                salesReport.Id, booking.Id, paymentEvent.PaymentId, saleAmount, currency, salesReport.Items.Count);

            return salesReport;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "Concurrent duplicate detected when saving SalesReport for Payment {PaymentId} or Booking {BookingId}. Fetching existing record.",
                paymentEvent.PaymentId, booking.Id);

            _dbContext.Entry(salesReport).State = EntityState.Detached;
            foreach (var item in salesReport.Items)
            {
                _dbContext.Entry(item).State = EntityState.Detached;
            }

            var existing = await _dbContext.SalesReports
                .Include(s => s.Items)
                .Include(s => s.RefundAdjustments)
                .FirstOrDefaultAsync(s => s.PaymentId == paymentEvent.PaymentId || s.BookingId == paymentEvent.BookingId, cancellationToken);

            if (existing != null)
            {
                return existing;
            }

            throw;
        }
    }

    public async Task<SalesReport?> ApplyRefundAdjustmentAsync(
        Guid bookingId,
        Guid refundId,
        decimal refundAmount,
        string refundStatus,
        string? reason = null,
        DateTimeOffset? refundedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EP-383: Applying refund adjustment for Booking {BookingId}, RefundId {RefundId}, Amount {Amount}, Status {Status}",
            bookingId, refundId, refundAmount, refundStatus);

        var report = await _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .FirstOrDefaultAsync(s => s.BookingId == bookingId, cancellationToken);

        if (report == null)
        {
            _logger.LogWarning(
                "Cannot apply refund {RefundId}: No SalesReport found for Booking {BookingId}",
                refundId, bookingId);
            return null;
        }

        // Idempotency: avoid double-applying the same refund adjustment
        if (report.RefundAdjustments.Any(r => r.RefundId == refundId))
        {
            _logger.LogInformation(
                "Refund {RefundId} has already been applied to SalesReport {SaleId} for Booking {BookingId}. Skipping duplicate.",
                refundId, report.Id, bookingId);
            return report;
        }

        var isPending = string.Equals(refundStatus, "pending", StringComparison.OrdinalIgnoreCase);

        if (isPending)
        {
            var pendingAdj = new SaleRefundAdjustment
            {
                Id = Guid.NewGuid(),
                SalesReportId = report.Id,
                RefundId = refundId,
                Amount = refundAmount,
                Reason = reason,
                Status = "Pending",
                AdjustedAtUtc = refundedAtUtc ?? DateTimeOffset.UtcNow
            };
            _dbContext.SaleRefundAdjustments.Add(pendingAdj);

            if (report.RefundStatus == SalesRefundStatus.None)
            {
                report.RefundStatus = SalesRefundStatus.Pending;
            }

            report.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Recorded pending refund {RefundId} on SalesReport {SaleId} for Booking {BookingId}",
                refundId, report.Id, bookingId);
            return report;
        }

        // Authoritative completed refund:
        var completedAdj = new SaleRefundAdjustment
        {
            Id = Guid.NewGuid(),
            SalesReportId = report.Id,
            RefundId = refundId,
            Amount = refundAmount,
            Reason = reason,
            Status = "Completed",
            AdjustedAtUtc = refundedAtUtc ?? DateTimeOffset.UtcNow
        };
        _dbContext.SaleRefundAdjustments.Add(completedAdj);

        // Historical sale record is PRESERVED, never deleted!
        // We only adjust refunded amounts and status:
        report.RefundedAmount = Math.Min(report.SaleAmount, report.RefundedAmount + refundAmount);

        if (report.RefundedAmount >= report.SaleAmount)
        {
            report.RefundStatus = SalesRefundStatus.Refunded;
        }
        else if (report.RefundedAmount > 0)
        {
            report.RefundStatus = SalesRefundStatus.PartiallyRefunded;
        }

        report.RefundedAtUtc = refundedAtUtc ?? DateTimeOffset.UtcNow;
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;

        // Allocate refund across line items
        if (report.RefundStatus == SalesRefundStatus.Refunded)
        {
            // Full refund
            foreach (var item in report.Items)
            {
                item.RefundedAmount = item.Subtotal;
                item.RefundedQuantity = item.Quantity;
            }
        }
        else
        {
            // Partial refund allocation
            decimal remainingToAllocate = refundAmount;
            foreach (var item in report.Items)
            {
                if (remainingToAllocate <= 0) break;
                decimal available = item.Subtotal - item.RefundedAmount;
                if (available <= 0) continue;

                decimal allocation = Math.Min(available, remainingToAllocate);
                item.RefundedAmount += allocation;
                remainingToAllocate -= allocation;

                if (item.UnitPrice > 0)
                {
                    item.RefundedQuantity = (int)Math.Min(item.Quantity, Math.Floor(item.RefundedAmount / item.UnitPrice));
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully updated SalesReport {SaleId} for Booking {BookingId} with completed refund {RefundId}. RefundedAmount: {RefundedAmount}, Status: {Status}",
            report.Id, bookingId, refundId, report.RefundedAmount, report.RefundStatus);

        return report;
    }

    public async Task<SalesReport?> GetSalesReportByIdAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);
    }

    public async Task<SalesReport?> GetSalesReportByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.BookingId == bookingId, cancellationToken);
    }

    public async Task<SalesReport?> GetSalesReportByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PaymentId == paymentId, cancellationToken);
    }

    public async Task<IReadOnlyList<SalesReport>> GetSalesReportsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .OrderByDescending(s => s.PaymentTimestampUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalesReport>> GetSalesReportsByOrganizerIdAsync(Guid organizerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SalesReports
            .Include(s => s.Items)
            .Include(s => s.RefundAdjustments)
            .AsNoTracking()
            .Where(s => s.OrganizerId == organizerId)
            .OrderByDescending(s => s.PaymentTimestampUtc)
            .ToListAsync(cancellationToken);
    }
}
