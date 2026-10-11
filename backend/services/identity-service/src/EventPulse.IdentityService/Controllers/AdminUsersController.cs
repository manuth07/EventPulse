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

    /// <summary>
    /// PATCH/POST /api/admin/users/{userId}/activate
    /// Activates a previously deactivated or suspended user account.
    /// </summary>
    [HttpPatch("users/{userId}/activate")]
    [HttpPost("users/{userId}/activate")]
    [HttpPut("users/{userId}/activate")]
    public async Task<IActionResult> ActivateUser(
        [FromRoute] string userId,
        [FromBody] UserStatusChangeRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var targetGuid) || targetGuid == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_USER_ID", message = "The specified user ID format is invalid." });
        }

        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid caller identity." });
        }

        var result = await _adminService.ActivateUserAsync(
            callerId,
            targetGuid,
            request?.Reason,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(new { message = result.Message, user = result.User });
    }

    /// <summary>
    /// PATCH/POST /api/admin/users/{userId}/deactivate
    /// Deactivates a user account, enforcing self-deactivation and last active admin guards.
    /// </summary>
    [HttpPatch("users/{userId}/deactivate")]
    [HttpPost("users/{userId}/deactivate")]
    [HttpPut("users/{userId}/deactivate")]
    public async Task<IActionResult> DeactivateUser(
        [FromRoute] string userId,
        [FromBody] UserStatusChangeRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var targetGuid) || targetGuid == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_USER_ID", message = "The specified user ID format is invalid." });
        }

        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid caller identity." });
        }

        var result = await _adminService.DeactivateUserAsync(
            callerId,
            targetGuid,
            request?.Reason,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(new { message = result.Message, user = result.User });
    }

    /// <summary>
    /// PATCH/PUT/POST /api/admin/users/{userId}/role
    /// Promotes or demotes an account's role with self-demotion and last active admin guards.
    /// </summary>
    [HttpPatch("users/{userId}/role")]
    [HttpPut("users/{userId}/role")]
    [HttpPut("users/{userId}/roles")]
    [HttpPost("users/{userId}/role")]
    public async Task<IActionResult> ChangeUserRole(
        [FromRoute] string userId,
        [FromBody] UserRoleChangeRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var targetGuid) || targetGuid == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_USER_ID", message = "The specified user ID format is invalid." });
        }

        var targetRole = request?.Role ?? request?.Roles?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(targetRole))
        {
            return BadRequest(new { code = "INVALID_ROLE_SPECIFIED", message = "Role name cannot be empty." });
        }

        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid caller identity." });
        }

        var result = await _adminService.ChangeUserRoleAsync(
            callerId,
            targetGuid,
            targetRole,
            request?.Reason,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Message });
        }

        return Ok(new { message = result.Message, user = result.User });
    }

    /// <summary>
    /// PUT/PATCH /api/admin/users/{userId}/status
    /// Combined status endpoint supporting both activation and deactivation.
    /// </summary>
    [HttpPut("users/{userId}/status")]
    [HttpPatch("users/{userId}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        [FromRoute] string userId,
        [FromBody] UserStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.IsActive.HasValue)
        {
            return BadRequest(new { code = "INVALID_REQUEST", message = "isActive field is required." });
        }

        return request.IsActive.Value
            ? await ActivateUser(userId, request, cancellationToken)
            : await DeactivateUser(userId, request, cancellationToken);
    }

    private bool TryGetCallerId(out Guid callerId)
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(idStr, out callerId);
    }
}
