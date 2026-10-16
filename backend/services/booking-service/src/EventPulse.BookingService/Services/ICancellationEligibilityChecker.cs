using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;

namespace EventPulse.BookingService.Services;

/// <summary>
/// Reusable cancellation eligibility checker enforcing EventPulse booking cancellation rules (EP-362).
/// </summary>
public interface ICancellationEligibilityChecker
{
    /// <summary>
    /// Pure synchronous validation of a booking instance against customer ownership, status, and event date.
    /// </summary>
    CancellationEligibilityResult EvaluateEligibility(
        Booking booking,
        Guid customerId,
        DateTimeOffset eventDateUtc);

    /// <summary>
    /// Asynchronous evaluation fetching booking from database and event schedule from EventService.
    /// </summary>
    Task<CancellationEligibilityResult> EvaluateEligibilityAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronous evaluation with an explicit or pre-fetched event date.
    /// </summary>
    Task<CancellationEligibilityResult> EvaluateEligibilityAsync(
        Guid bookingId,
        Guid customerId,
        DateTimeOffset? explicitEventDateUtc,
        CancellationToken cancellationToken = default);
}
