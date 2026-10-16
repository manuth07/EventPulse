using EventPulse.IdentityService.DTOs;

namespace EventPulse.IdentityService.Services;

/// <summary>
/// Service providing administrative user search, inspection, and platform overview operations.
/// </summary>
public interface IAdminManagementService
{
    /// <summary>
    /// Retrieves a paginated list of registered users matching specified filters and ordering.
    /// </summary>
    Task<PagedResult<AdminUserSummaryDto>> GetUsersAsync(
        AdminUserQueryParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves full safe operational details for an individual user account.
    /// Returns null if the user does not exist.
    /// </summary>
    Task<AdminUserDetailsDto?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves approved platform overview metrics respecting microservice domain ownership.
    /// </summary>
    Task<AdminPlatformOverviewDto> GetPlatformOverviewAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a deactivated user account.
    /// </summary>
    Task<AdminActionResult> ActivateUserAsync(
        Guid actorAdminId,
        Guid targetUserId,
        string? reason = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates an active user account with safety guards for self-deactivation and last active admin protection.
    /// </summary>
    Task<AdminActionResult> DeactivateUserAsync(
        Guid actorAdminId,
        Guid targetUserId,
        string? reason = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user's system role with safety guards for self-demotion and last active admin protection.
    /// </summary>
    Task<AdminActionResult> ChangeUserRoleAsync(
        Guid actorAdminId,
        Guid targetUserId,
        string newRole,
        string? reason = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}
