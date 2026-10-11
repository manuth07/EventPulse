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
}
