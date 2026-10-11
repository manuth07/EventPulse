namespace EventPulse.BookingService.DTOs;

/// <summary>
/// Standardized cancellation reason codes defined by the EventPulse Cancellation Policy (EP-361 / EP-362).
/// </summary>
public static class CancellationReasonCodes
{
    public const string AlreadyCancelled = "ALREADY_CANCELLED";
    public const string CancellationWindowClosed = "CANCELLATION_WINDOW_CLOSED";
    public const string EventAlreadyStarted = "EVENT_ALREADY_STARTED";
    public const string InvalidBookingStatus = "INVALID_BOOKING_STATUS";
    public const string PaymentNotEligible = "PAYMENT_NOT_ELIGIBLE";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string BookingNotFound = "BOOKING_NOT_FOUND";
}

/// <summary>
/// Internal domain result from the cancellation eligibility checker.
/// </summary>
public record CancellationEligibilityResult(
    bool IsEligible,
    string? ReasonCode = null,
    string? Explanation = null,
    DateTimeOffset? CutoffUtc = null,
    TimeSpan? TimeRemaining = null)
{
    public static CancellationEligibilityResult Eligible(DateTimeOffset? cutoffUtc = null, TimeSpan? timeRemaining = null) =>
        new(true, null, "Booking is eligible for cancellation", cutoffUtc, timeRemaining);

    public static CancellationEligibilityResult Ineligible(
        string reasonCode,
        string explanation,
        DateTimeOffset? cutoffUtc = null,
        TimeSpan? timeRemaining = null) =>
        new(false, reasonCode, explanation, cutoffUtc, timeRemaining);
}

/// <summary>
/// Public API response DTO for cancellation eligibility check requests (EP-362).
/// </summary>
public record CancellationEligibilityResponseDto(
    bool IsEligible,
    string? ReasonCode = null,
    string? Explanation = null,
    DateTimeOffset? CutoffUtc = null,
    TimeSpan? TimeRemaining = null);
