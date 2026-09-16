using EventPulse.EventService.DTOs;

namespace EventPulse.EventService.Services;

/// <summary>
/// Service contract for Administrator event review operations.
/// Owns the lifecycle transitions:
///   Pending -> Approved
///   Pending -> Rejected
/// </summary>
public interface IEventReviewService
{
    /// <summary>
    /// Retrieves all events awaiting Administrator review (Status == Pending),
    /// ordered chronologically (oldest submissions first so admins review in order).
    /// </summary>
    Task<IReadOnlyList<AdminEventReviewDto>> GetPendingEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single pending event submission by ID for detailed review.
    /// Returns null if not found or if the event is not in Pending status.
    /// </summary>
    Task<AdminEventReviewDto?> GetPendingEventByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a Pending event submission.
    /// Transitions: Pending -> Approved.
    /// Returns conflict error if event is not currently in Pending status.
    /// </summary>
    Task<(AdminEventReviewDto? Result, string? Error, bool IsNotFound)> ApproveEventAsync(
        Guid id,
        Guid reviewerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a Pending event submission.
    /// Transitions: Pending -> Rejected.
    /// Returns conflict error if event is not currently in Pending status.
    /// </summary>
    Task<(AdminEventReviewDto? Result, string? Error, bool IsNotFound)> RejectEventAsync(
        Guid id,
        Guid reviewerId,
        string? notes = null,
        CancellationToken cancellationToken = default);
}
