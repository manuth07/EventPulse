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
}
