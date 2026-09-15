using EventPulse.EventService.DTOs;

namespace EventPulse.EventService.Services;

/// <summary>
/// Business logic contract for submitting and retrieving Event Cancellation Requests (EP-35 US-15).
/// </summary>
public interface IEventCancellationRequestService
{
    /// <summary>
    /// Submits a cancellation request for an Approved or Published event.
    /// The live Event record is NOT cancelled; an EventCancellationRequest in Pending status is created.
    /// Returns:
    ///   - (Result, null, false, false, false, false) on success
    ///   - (null, error, isNotFound=true, false, false, false) if event not found
    ///   - (null, error, false, isForbidden=true, false, false) if caller is not the owner
    ///   - (null, error, false, false, isInvalidState=true, false) if event is not in Approved/Published status
    ///   - (null, error, false, false, false, isConflict=true) if a Pending cancellation request or update request already exists
    /// </summary>
    Task<(EventCancellationRequestDto? Result, string? Error, bool IsNotFound, bool IsForbidden, bool IsInvalidState, bool IsConflict)> SubmitCancellationRequestAsync(
        Guid eventId,
        SubmitEventCancellationRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the active (or latest) cancellation request for an event.
    /// Returns:
    ///   - (Result, null, false, false) on success
    ///   - (null, error, isNotFound=true, false) if event not found or no request exists
    ///   - (null, error, false, isForbidden=true) if caller is not the event owner
    /// </summary>
    Task<(EventCancellationRequestDto? Result, string? Error, bool IsNotFound, bool IsForbidden)> GetCancellationRequestAsync(
        Guid eventId,
        Guid organizerId,
        CancellationToken cancellationToken = default);
}
