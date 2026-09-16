using EventPulse.EventService.DTOs;

namespace EventPulse.EventService.Services;

/// <summary>
/// Handles event submission business rules including poster upload.
/// Returns (dto, null) on success, (null, errorMessage) on validation/business failure.
/// </summary>
public interface IEventSubmissionService
{
    Task<(EventSubmissionResponseDto? Result, string? Error)> CreateAsync(
        CreateEventRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all event submissions belonging to the given organizer, ordered newest first.
    /// Exposes all lifecycle statuses with resolved image URLs.
    /// </summary>
    Task<IReadOnlyList<OrganizerEventSubmissionDto>> GetOrganizerSubmissionsAsync(
        Guid organizerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single event submission belonging to the given organizer.
    /// Returns null if not found or not owned by organizer.
    /// </summary>
    Task<OrganizerEventSubmissionDto?> GetOrganizerSubmissionByIdAsync(
        Guid eventId,
        Guid organizerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits and resubmits a Rejected event, transitioning it back to Pending.
    /// Preserves existing Event ID, previous review notes, and handles poster replacement.
    /// </summary>
    Task<(OrganizerEventSubmissionDto? Result, string? Error, bool IsNotFound, bool IsForbidden, bool IsInvalidState)> ResubmitAsync(
        Guid eventId,
        ResubmitEventRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default);
}
