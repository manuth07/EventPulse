using Microsoft.EntityFrameworkCore;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Events;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

public class BookingCancellationService : IBookingCancellationService
{
    private readonly BookingDbContext _dbContext;
    private readonly IBookingEventPublisher _eventPublisher;
    private readonly ILogger<BookingCancellationService> _logger;

    public BookingCancellationService(
        BookingDbContext dbContext,
        IBookingEventPublisher eventPublisher,
        ILogger<BookingCancellationService> logger)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<(bool IsEligible, string? Reason, Booking? Booking)> EvaluateCancellationEligibilityAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken ct = default)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.Items)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null || booking.CustomerId != customerId)
        {
            _logger.LogWarning("Cancellation eligibility failed for BookingId {BookingId}, CustomerId {CustomerId}: Booking not found or access denied.", bookingId, customerId);
            return (false, "Booking not found or access denied", null);
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            _logger.LogInformation("Cancellation eligibility failed for BookingId {BookingId}: Booking is already cancelled.", bookingId);
            return (false, "Booking is already cancelled", booking);
        }

        if (booking.Status == BookingStatus.PaymentFailed)
        {
            _logger.LogInformation("Cancellation eligibility failed for BookingId {BookingId}: Cannot cancel a failed payment booking.", bookingId);
            return (false, "Cannot cancel a failed payment booking", booking);
        }

        return (true, null, booking);
    }

    public async Task<BookingCancellationResultDto> CancelBookingAsync(
        Guid bookingId,
        Guid customerId,
        CancelBookingRequest? request,
        CancellationToken ct = default)
    {
        var (isEligible, reason, booking) = await EvaluateCancellationEligibilityAsync(bookingId, customerId, ct);

        if (!isEligible || booking == null)
        {
            return new BookingCancellationResultDto(
                bookingId: bookingId,
                bookingReference: booking?.BookingReference ?? string.Empty,
                previousStatus: booking?.Status.ToString() ?? string.Empty,
                newStatus: booking?.Status.ToString() ?? string.Empty,
                cancelledAt: DateTimeOffset.UtcNow,
                success: false,
                message: reason ?? "Booking is not eligible for cancellation"
            );
        }

        var previousStatus = booking.Status.ToString();
        booking.Status = BookingStatus.Cancelled;

        if (booking.Tickets != null)
        {
            foreach (var ticket in booking.Tickets)
            {
                ticket.Status = TicketStatus.Cancelled;
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // Publish outbound domain event to release capacity and notify downstream services
        // Note: Capacity restoration is handled asynchronously via Kafka event emission (topic: booking-cancelled).
        var cancelledAt = DateTimeOffset.UtcNow;
        var releasedTickets = booking.Items
            .Select(i => new CancelledTicketItemDto(i.TicketTypeId, i.Quantity))
            .ToList();

        var cancelledEvent = new BookingCancelledEvent
        {
            BookingId = booking.Id,
            BookingReference = booking.BookingReference,
            CustomerId = booking.CustomerId,
            EventId = booking.EventId,
            ReleasedTickets = releasedTickets,
            CancelledAt = cancelledAt,
            Items = booking.Items.Select(i => new BookingItemDto
            {
                TicketTypeId = i.TicketTypeId,
                TicketName = i.TicketName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal
            }).ToList()
        };

        await _eventPublisher.PublishBookingCancelledAsync(cancelledEvent, ct);

        // If booking was Confirmed and has a paid amount, emit BookingRefundRequestedEvent
        if (previousStatus == BookingStatus.Confirmed.ToString() && booking.TotalAmount > 0)
        {
            var refundEvent = new EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent(
                RefundRequestId: Guid.NewGuid(),
                BookingId: booking.Id,
                BookingReference: booking.BookingReference,
                CustomerId: booking.CustomerId,
                EventId: booking.EventId,
                RefundAmount: booking.TotalAmount,
                Reason: request?.Reason ?? "Booking cancelled by customer",
                RequestedAt: cancelledAt
            );

            await _eventPublisher.PublishBookingRefundRequestedAsync(refundEvent, ct);
        }

        _logger.LogInformation("Booking {BookingId} ({BookingReference}) cancelled successfully by customer {CustomerId}. Reason: {Reason}. Emitted BookingCancelledEvent with {TicketTypeCount} ticket types released.",
            booking.Id, booking.BookingReference, customerId, request?.Reason ?? "None", releasedTickets.Count);

        return new BookingCancellationResultDto(
            bookingId: booking.Id,
            bookingReference: booking.BookingReference,
            previousStatus: previousStatus,
            newStatus: BookingStatus.Cancelled.ToString(),
            cancelledAt: cancelledAt,
            success: true,
            message: "Booking cancelled successfully"
        );
    }

    public async Task<TicketCancellationResultDto> CancelSingleTicketAsync(
        Guid ticketId,
        Guid customerId,
        string? reason = default,
        CancellationToken ct = default)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Booking)
                .ThenInclude(b => b!.Tickets)
            .Include(t => t.Booking)
                .ThenInclude(b => b!.Items)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket == null || ticket.Booking == null || ticket.Booking.CustomerId != customerId)
        {
            _logger.LogWarning("Single ticket cancellation failed for TicketId {TicketId}, CustomerId {CustomerId}: Ticket not found or access denied.",
                ticketId, customerId);
            return new TicketCancellationResultDto(
                ticketId,
                ticket?.TicketCode ?? string.Empty,
                ticket?.Status.ToString() ?? string.Empty,
                ticket?.Booking?.Status.ToString() ?? string.Empty,
                false,
                "Ticket not found or access denied");
        }

        if (ticket.Status != TicketStatus.Valid)
        {
            _logger.LogInformation("Single ticket cancellation rejected for TicketId {TicketId}: Ticket status is {Status} (cannot cancel).",
                ticketId, ticket.Status);
            return new TicketCancellationResultDto(
                ticket.Id,
                ticket.TicketCode,
                ticket.Status.ToString(),
                ticket.Booking.Status.ToString(),
                false,
                $"Ticket is already {ticket.Status}");
        }

        var parentWasConfirmed = ticket.Booking.Status == BookingStatus.Confirmed;
        ticket.Status = TicketStatus.Cancelled;

        // Check if any valid tickets remain in parent booking
        var hasRemainingValidTickets = ticket.Booking.Tickets.Any(t => t.Id != ticket.Id && t.Status == TicketStatus.Valid);
        if (!hasRemainingValidTickets)
        {
            ticket.Booking.Status = BookingStatus.Cancelled;
            _logger.LogInformation("All tickets for Booking {BookingId} are now cancelled. Booking status transitioned to Cancelled.",
                ticket.Booking.Id);
        }

        await _dbContext.SaveChangesAsync(ct);

        // Emit TicketCancelledEvent for inventory/capacity restoration
        var cancelledEvent = new TicketCancelledEvent
        {
            TicketId = ticket.Id,
            BookingId = ticket.Booking.Id,
            BookingReference = ticket.Booking.BookingReference,
            CustomerId = customerId,
            EventId = ticket.EventId,
            TicketTypeId = ticket.TicketTypeId,
            TicketCode = ticket.TicketCode,
            CancelledAt = DateTimeOffset.UtcNow,
            Reason = reason
        };

        await _eventPublisher.PublishTicketCancelledAsync(cancelledEvent, ct);

        // Emit BookingRefundRequestedEvent for this single ticket if parent booking was paid/confirmed
        var item = ticket.Booking.Items?.FirstOrDefault(i => i.TicketTypeId == ticket.TicketTypeId);
        var refundAmount = item?.UnitPrice ?? 0m;
        if (refundAmount == 0m && item != null && item.Quantity > 0 && item.Subtotal > 0)
        {
            refundAmount = item.Subtotal / item.Quantity;
        }

        if (parentWasConfirmed && refundAmount > 0)
        {
            var refundEvent = new EventPulse.Contracts.Kafka.Events.BookingRefundRequestedEvent(
                RefundRequestId: Guid.NewGuid(),
                BookingId: ticket.Booking.Id,
                BookingReference: ticket.Booking.BookingReference,
                CustomerId: customerId,
                EventId: ticket.EventId,
                RefundAmount: refundAmount,
                Reason: reason ?? $"Individual ticket {ticket.TicketCode} cancelled by customer",
                RequestedAt: DateTimeOffset.UtcNow
            );

            await _eventPublisher.PublishBookingRefundRequestedAsync(refundEvent, ct);
        }

        _logger.LogInformation("Ticket {TicketId} ({TicketCode}) cancelled successfully by customer {CustomerId}. Parent booking status: {BookingStatus}.",
            ticket.Id, ticket.TicketCode, customerId, ticket.Booking.Status);

        return new TicketCancellationResultDto(
            ticket.Id,
            ticket.TicketCode,
            TicketStatus.Cancelled.ToString(),
            ticket.Booking.Status.ToString(),
            true,
            "Ticket cancelled successfully");
    }
}
