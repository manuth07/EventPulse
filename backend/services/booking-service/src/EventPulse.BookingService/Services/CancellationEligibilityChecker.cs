using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

/// <summary>
/// Authoritative implementation of the cancellation eligibility checker (EP-362).
/// Enforces booking status, event cutoff boundary, payment status, already-cancelled state,
/// and ownership security using UTC instants and an injectable clock.
/// </summary>
public class CancellationEligibilityChecker : ICancellationEligibilityChecker
{
    private readonly BookingDbContext _dbContext;
    private readonly IEventAvailabilityClient _eventAvailabilityClient;
    private readonly ILogger<CancellationEligibilityChecker> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _cutoffWindow;

    /// <summary>
    /// Default cutoff duration before event start (48 hours per approved baseline policy).
    /// </summary>
    public static readonly TimeSpan DefaultCutoffWindow = TimeSpan.FromHours(48);

    public CancellationEligibilityChecker(
        BookingDbContext dbContext,
        IEventAvailabilityClient eventAvailabilityClient,
        ILogger<CancellationEligibilityChecker> logger,
        TimeProvider? timeProvider = null,
        TimeSpan? cutoffWindow = null)
    {
        _dbContext = dbContext;
        _eventAvailabilityClient = eventAvailabilityClient;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _cutoffWindow = cutoffWindow ?? DefaultCutoffWindow;
    }

    /// <summary>
    /// Pure synchronous validation of a booking instance against customer ownership, status, and event date.
    /// </summary>
    public CancellationEligibilityResult EvaluateEligibility(
        Booking booking,
        Guid customerId,
        DateTimeOffset eventDateUtc)
    {
        if (booking == null)
        {
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.BookingNotFound,
                "Booking not found");
        }

        if (booking.CustomerId != customerId)
        {
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.Unauthorized,
                "Booking not found or access denied");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.AlreadyCancelled,
                "Booking is already cancelled");
        }

        if (booking.Status == BookingStatus.PaymentFailed)
        {
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.PaymentNotEligible,
                "Cannot cancel a failed payment booking");
        }

        if (!Enum.IsDefined(typeof(BookingStatus), booking.Status) ||
            (booking.Status != BookingStatus.Confirmed && booking.Status != BookingStatus.PendingPayment))
        {
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.InvalidBookingStatus,
                "Booking status is not eligible for cancellation");
        }

        var now = _timeProvider.GetUtcNow();
        var timeRemaining = eventDateUtc - now;
        var cutoffUtc = eventDateUtc - _cutoffWindow;

        // Post-start or concluded event: strictly prohibited
        if (timeRemaining <= TimeSpan.Zero)
        {
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.EventAlreadyStarted,
                "Cannot cancel tickets for an event that has already started or concluded",
                cutoffUtc,
                timeRemaining);
        }

        // Cutoff boundary: at exactly cutoff (timeRemaining == cutoff) or after cutoff (timeRemaining < cutoff)
        if (timeRemaining <= _cutoffWindow)
        {
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.CancellationWindowClosed,
                "The cancellation window for this event has closed",
                cutoffUtc,
                timeRemaining);
        }

        return CancellationEligibilityResult.Eligible(cutoffUtc, timeRemaining);
    }

    /// <summary>
    /// Asynchronous evaluation fetching booking from database and event schedule from EventService.
    /// </summary>
    public Task<CancellationEligibilityResult> EvaluateEligibilityAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return EvaluateEligibilityAsync(bookingId, customerId, explicitEventDateUtc: null, cancellationToken);
    }

    /// <summary>
    /// Asynchronous evaluation with an explicit or pre-fetched event date.
    /// </summary>
    public async Task<CancellationEligibilityResult> EvaluateEligibilityAsync(
        Guid bookingId,
        Guid customerId,
        DateTimeOffset? explicitEventDateUtc,
        CancellationToken cancellationToken = default)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.Items)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            _logger.LogWarning("Cancellation eligibility check failed: Booking {BookingId} not found.", bookingId);
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.BookingNotFound,
                "Booking not found");
        }

        if (booking.CustomerId != customerId)
        {
            _logger.LogWarning(
                "Cancellation eligibility check failed: Customer {CustomerId} unauthorized for Booking {BookingId}.",
                customerId, bookingId);
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.Unauthorized,
                "Booking not found or access denied");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            _logger.LogInformation("Cancellation eligibility check failed for Booking {BookingId}: Already cancelled.", bookingId);
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.AlreadyCancelled,
                "Booking is already cancelled");
        }

        if (booking.Status == BookingStatus.PaymentFailed)
        {
            _logger.LogInformation("Cancellation eligibility check failed for Booking {BookingId}: Payment failed.", bookingId);
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.PaymentNotEligible,
                "Cannot cancel a failed payment booking");
        }

        if (!Enum.IsDefined(typeof(BookingStatus), booking.Status) ||
            (booking.Status != BookingStatus.Confirmed && booking.Status != BookingStatus.PendingPayment))
        {
            _logger.LogInformation("Cancellation eligibility check failed for Booking {BookingId}: Invalid status {Status}.",
                bookingId, booking.Status);
            return CancellationEligibilityResult.Ineligible(
                CancellationReasonCodes.InvalidBookingStatus,
                "Booking status is not eligible for cancellation");
        }

        DateTimeOffset eventDateUtc;
        if (explicitEventDateUtc.HasValue)
        {
            eventDateUtc = explicitEventDateUtc.Value;
        }
        else
        {
            try
            {
                var eventSummary = await _eventAvailabilityClient.GetEventSummaryAsync(booking.EventId, cancellationToken);
                if (eventSummary != null)
                {
                    eventDateUtc = DateTime.SpecifyKind(eventSummary.EventDate, DateTimeKind.Utc);
                }
                else
                {
                    _logger.LogWarning(
                        "Event summary not found for EventId {EventId}. Bypassing cutoff date check for Booking {BookingId}.",
                        booking.EventId, bookingId);
                    return CancellationEligibilityResult.Eligible();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch event summary for EventId {EventId}", booking.EventId);
                return CancellationEligibilityResult.Eligible();
            }
        }

        return EvaluateEligibility(booking, customerId, eventDateUtc);
    }
}
