using EventPulse.EventService.DTOs;

namespace EventPulse.EventService.Services;

/// <summary>
/// Business logic contract for submitting and retrieving Event Update Requests (EP-34 US-14).
/// </summary>
public interface IEventUpdateRequestService
{
    /// <summary>
    /// Submits proposed modifications to an Approved or Published event.
    /// The live Event record is NOT modified; an EventUpdateRequest in Pending status is created.
    /// Returns:
    ///   - (Result, null, false, false, false) on success
    ///   - (null, error, isNotFound=true, false, false) if event not found
    ///   - (null, error, false, isForbidden=true, false) if caller is not the owner
    ///   - (null, error, false, false, isInvalidState=true) if event is not in Approved/Published status
    ///   - (null, error, false, false, isConflict=true) if a Pending update request already exists
    /// </summary>
    Task<(EventUpdateRequestDto? Result, string? Error, bool IsNotFound, bool IsForbidden, bool IsInvalidState, bool IsConflict)> SubmitUpdateRequestAsync(
        Guid eventId,
        SubmitEventUpdateRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the active (or latest) update request for an event.
    /// Returns:
    ///   - (Result, null, false, false) on success
    ///   - (null, error, isNotFound=true, false) if event not found or no request exists
    ///   - (null, error, false, isForbidden=true) if caller is not the event owner
    /// </summary>
    Task<(EventUpdateRequestDto? Result, string? Error, bool IsNotFound, bool IsForbidden)> GetUpdateRequestAsync(
        Guid eventId,
        Guid organizerId,
        CancellationToken cancellationToken = default);
}
