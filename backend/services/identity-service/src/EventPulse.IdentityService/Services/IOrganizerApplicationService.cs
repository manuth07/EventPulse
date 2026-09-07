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

    /// <summary>
    /// Resubmits a previously rejected organizer application.
    /// Updates the existing record and transitions it back to Pending.
    /// </summary>
    Task<(OrganizerApplicationDto? Result, string? Error, int StatusCode, string? ErrorCode)> ResubmitApplicationAsync(
        Guid userId,
        CreateOrganizerApplicationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves organizer applications for administrator review, optionally filtered by status.
    /// </summary>
    Task<List<AdminOrganizerApplicationDto>> GetAdminApplicationsAsync(
        Models.OrganizerApplicationStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single organizer application with review details for an administrator.
    /// </summary>
    Task<AdminOrganizerApplicationDto?> GetAdminApplicationByIdAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves an organizer application and atomically grants the Organizer role to the applicant.
    /// </summary>
    Task<(AdminOrganizerApplicationDto? Result, string? Error, int StatusCode, string? ErrorCode)> ApproveApplicationAsync(
        Guid applicationId,
        Guid adminId,
        ApproveOrganizerApplicationRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an organizer application with mandatory administrator feedback.
    /// </summary>
    Task<(AdminOrganizerApplicationDto? Result, string? Error, int StatusCode, string? ErrorCode)> RejectApplicationAsync(
        Guid applicationId,
        Guid adminId,
        RejectOrganizerApplicationRequest request,
        CancellationToken cancellationToken = default);
}
