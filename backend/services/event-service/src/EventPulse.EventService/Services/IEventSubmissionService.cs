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
}
