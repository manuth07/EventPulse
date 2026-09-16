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

    /// <summary>
    /// EP-35 / US-15: Retrieves all Event Cancellation Requests awaiting Administrator review (Status = Pending),
    /// ordered chronologically (oldest first).
    /// </summary>
    Task<IReadOnlyList<AdminEventCancellationReviewDto>> GetPendingCancellationRequestsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-35 / US-15: Retrieves a single Event Cancellation Request with live event details and ticket sales summary
    /// for Administrator review.
    /// </summary>
    Task<AdminEventCancellationReviewDto?> GetCancellationRequestReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-35 / US-15: Administrator approves an Event Cancellation Request.
    /// Atomically transitions the live Event to Cancelled, marks request as Approved,
    /// and records reviewer metadata.
    /// Returns:
    ///   - (Result, null, false, false) on success
    ///   - (null, error, isNotFound=true, false) if request or event not found
    ///   - (null, error, false, isInvalidState=true) if request is not in Pending status or event is already cancelled
    /// </summary>
    Task<(AdminEventCancellationReviewDto? Result, string? Error, bool IsNotFound, bool IsInvalidState)> ApproveCancellationRequestAsync(
        Guid requestId,
        Guid reviewerId,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-35 / US-15: Administrator rejects an Event Cancellation Request with mandatory review notes.
    /// Leaves live Event status completely unchanged (Approved or Published), updates request status to Rejected,
    /// and records reviewer metadata.
    /// Returns:
    ///   - (Result, null, false, false) on success
    ///   - (null, error, isNotFound=true, false) if request not found
    ///   - (null, error, false, isInvalidState=true) if request is not in Pending status or notes missing
    /// </summary>
    Task<(AdminEventCancellationReviewDto? Result, string? Error, bool IsNotFound, bool IsInvalidState)> RejectCancellationRequestAsync(
        Guid requestId,
        Guid reviewerId,
        string notes,
        CancellationToken cancellationToken = default);
}
