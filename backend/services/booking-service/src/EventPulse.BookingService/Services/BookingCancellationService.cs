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
    private readonly IEventAvailabilityClient _eventAvailabilityClient;
    private readonly ILogger<BookingCancellationService> _logger;

    private readonly ICancellationEligibilityChecker _eligibilityChecker;
    private readonly TimeProvider _timeProvider;

    public BookingCancellationService(
        BookingDbContext dbContext,
        IBookingEventPublisher eventPublisher,
        IEventAvailabilityClient eventAvailabilityClient,
        ILogger<BookingCancellationService> logger,
        ICancellationEligibilityChecker? eligibilityChecker = null,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
        _eventAvailabilityClient = eventAvailabilityClient;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _eligibilityChecker = eligibilityChecker ?? new CancellationEligibilityChecker(
            dbContext,
            eventAvailabilityClient,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CancellationEligibilityChecker>.Instance,
            _timeProvider);
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

        var eligibility = await _eligibilityChecker.EvaluateEligibilityAsync(bookingId, customerId, ct);
        if (!eligibility.IsEligible)
        {
            _logger.LogInformation("Cancellation eligibility failed for BookingId {BookingId}: {Reason} ({ReasonCode})",
                bookingId, eligibility.Explanation, eligibility.ReasonCode);
            return (false, eligibility.Explanation ?? eligibility.ReasonCode, booking);
        }

        return (true, null, booking);
    }

    public async Task<BookingCancellationResultDto> CancelBookingAsync(
        Guid bookingId,
        Guid customerId,
        CancelBookingRequest? request,
        CancellationToken ct = default)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.Items)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        var eligibility = await _eligibilityChecker.EvaluateEligibilityAsync(bookingId, customerId, ct);

        if (!eligibility.IsEligible || booking == null)
        {
            return new BookingCancellationResultDto(
                bookingId: bookingId,
                bookingReference: booking?.BookingReference ?? string.Empty,
                previousStatus: booking?.Status.ToString() ?? string.Empty,
                newStatus: booking?.Status.ToString() ?? string.Empty,
                cancelledAt: _timeProvider.GetUtcNow(),
                success: false,
                message: eligibility.Explanation ?? "Booking is not eligible for cancellation",
                cancelledBy: booking?.CancelledBy,
                reasonCode: eligibility.ReasonCode
            );
        }

        var previousStatus = booking.Status.ToString();
        var cancelledAt = _timeProvider.GetUtcNow();

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = cancelledAt;
        booking.CancelledBy = customerId.ToString();
        booking.CancellationReason = request?.Reason ?? "Booking cancelled by customer";

        if (booking.Tickets != null)
        {
            foreach (var ticket in booking.Tickets)
            {
                ticket.Status = TicketStatus.Cancelled;
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during cancellation of Booking {BookingId}. Checking current database state.", bookingId);

            var currentBooking = await _dbContext.Bookings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

            if (currentBooking != null && currentBooking.Status == BookingStatus.Cancelled)
            {
                _logger.LogInformation("Booking {BookingId} was already cancelled concurrently.", bookingId);
                return new BookingCancellationResultDto(
                    bookingId: currentBooking.Id,
                    bookingReference: currentBooking.BookingReference,
                    previousStatus: BookingStatus.Cancelled.ToString(),
                    newStatus: BookingStatus.Cancelled.ToString(),
                    cancelledAt: currentBooking.CancelledAt ?? _timeProvider.GetUtcNow(),
                    success: false,
                    message: "Booking is already cancelled",
                    cancelledBy: currentBooking.CancelledBy,
                    reasonCode: CancellationReasonCodes.AlreadyCancelled
                );
            }

            throw;
        }

        // EP-401: Restore inventory if booking was confirmed (sold), or release reservation if pending payment
        if (previousStatus == BookingStatus.Confirmed.ToString())
        {
            var cancellationKey = $"booking-{booking.Id}-cancelled";
            var itemsToRestore = booking.Items
                .Select(i => new ReserveInventoryItemDto { TicketTypeId = i.TicketTypeId, Quantity = i.Quantity })
                .ToList();

            try
            {
                await _eventAvailabilityClient.RestoreInventoryAsync(
                    booking.EventId,
                    booking.Id,
                    cancellationKey,
                    itemsToRestore,
                    ticketId: null,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to call RestoreInventoryAsync via REST client for Booking {BookingId}. Kafka event will serve as asynchronous compensation.", booking.Id);
            }
        }
        else if (previousStatus == BookingStatus.PendingPayment.ToString())
        {
            try
            {
                await _eventAvailabilityClient.ReleaseInventoryAsync(
                    booking.EventId,
                    booking.Id,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to call ReleaseInventoryAsync via REST client for Booking {BookingId}. Kafka event will serve as asynchronous compensation.", booking.Id);
            }
        }

        // Publish outbound domain event to release capacity and notify downstream services
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
            message: "Booking cancelled successfully",
            cancelledBy: booking.CancelledBy,
            reasonCode: null
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

        // EP-401: Restore inventory if parent booking was Confirmed
        if (parentWasConfirmed)
        {
            var cancellationKey = $"ticket-{ticket.Id}-cancelled";
            var itemToRestore = new List<ReserveInventoryItemDto>
            {
                new() { TicketTypeId = ticket.TicketTypeId, Quantity = 1 }
            };

            await _eventAvailabilityClient.RestoreInventoryAsync(
                ticket.EventId,
                ticket.Booking.Id,
                cancellationKey,
                itemToRestore,
                ticketId: ticket.Id,
                ct);
        }

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
            CancelledAt = _timeProvider.GetUtcNow(),
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
                RequestedAt: _timeProvider.GetUtcNow()
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
