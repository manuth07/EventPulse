using EventPulse.IdentityService.DTOs;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Service handling customer organizer applications.
/// </summary>
public interface IOrganizerApplicationService
{
    /// <summary>
    /// Submits a new organizer application for the authenticated user.
    /// Rejects if the user is already an Organizer or has an existing application.
    /// </summary>
    Task<(OrganizerApplicationDto? Result, string? Error, int StatusCode, string? ErrorCode)> SubmitApplicationAsync(
        Guid userId,
        CreateOrganizerApplicationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the organizer application for the authenticated user.
    /// </summary>
    Task<OrganizerApplicationDto?> GetMyApplicationAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
