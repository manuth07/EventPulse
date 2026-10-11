using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Security;
using EventPulse.IdentityService.Services;

namespace EventPulse.IdentityService.Controllers;

/// <summary>
/// Administrative controller for user supervision and platform information (EP-57 / EP-370).
/// Strictly restricted to authenticated users with active Administrator role in Identity database.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = AppPolicies.AdminOnly)]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminManagementService _adminService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        IAdminManagementService adminService,
        ILogger<AdminUsersController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/users
    /// Retrieves a paginated list of registered users with search, role, status filtering, and sorting.
    /// Password hashes, security tokens, and authentication secrets are never exposed.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] AdminUserQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminService.GetUsersAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/users/{userId}
    /// Retrieves safe operational details of an individual user account.
    /// </summary>
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUserDetails(
        [FromRoute] string userId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var parsedGuid) || parsedGuid == Guid.Empty)
        {
            return BadRequest(new
            {
                code = "INVALID_USER_ID",
                message = "The specified user ID format is invalid."
            });
        }

        var user = await _adminService.GetUserByIdAsync(parsedGuid, cancellationToken);
        if (user == null)
        {
            return NotFound(new
            {
                code = "USER_NOT_FOUND",
                message = $"User with ID '{userId}' was not found."
            });
        }

        return Ok(user);
    }

    /// <summary>
    /// GET /api/admin/overview
    /// Retrieves high-level platform overview metrics approved in EP-368.
    /// Respects service ownership boundaries without querying other service databases directly.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken = default)
    {
        var overview = await _adminService.GetPlatformOverviewAsync(cancellationToken);
        return Ok(overview);
    }
}
