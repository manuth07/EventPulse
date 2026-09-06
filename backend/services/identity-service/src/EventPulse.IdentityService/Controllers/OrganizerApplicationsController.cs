using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.IdentityService.DTOs;
using EventPulse.IdentityService.Models;
using EventPulse.IdentityService.Services;

namespace EventPulse.IdentityService.Controllers;

/// <summary>
/// Handles Organizer Applications for authenticated customers.
/// </summary>
[ApiController]
[Route("api/organizer-applications")]
[Authorize]
public class OrganizerApplicationsController : ControllerBase
{
    private readonly IOrganizerApplicationService _applicationService;
    private readonly ILogger<OrganizerApplicationsController> _logger;

    public OrganizerApplicationsController(
        IOrganizerApplicationService applicationService,
        ILogger<OrganizerApplicationsController> logger)
    {
        _applicationService = applicationService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/organizer-applications
    /// Submits a new organizer application for the currently authenticated user.
    /// User identity is extracted from the JWT token and can never be overridden by client request.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitApplication(
        [FromBody] CreateOrganizerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new
            {
                code = "VALIDATION_ERROR",
                message = "Validation failed.",
                errors
            });
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = "Invalid or missing user identity in authentication token."
            });
        }

        var (result, error, statusCode, errorCode) = await _applicationService.SubmitApplicationAsync(
            userId, request, cancellationToken);

        if (error is not null)
        {
            return StatusCode(statusCode, new
            {
                code = errorCode ?? "APPLICATION_ERROR",
                message = error
            });
        }

        return CreatedAtAction(nameof(GetMyApplication), new { }, result);
    }

    /// <summary>
    /// GET /api/organizer-applications/me
    /// Retrieves the current authenticated user's organizer application.
    /// Scoped strictly to the authenticated user derived from the JWT.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyApplication(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = "Invalid or missing user identity in authentication token."
            });
        }

        var application = await _applicationService.GetMyApplicationAsync(userId, cancellationToken);

        if (application is null)
        {
            return NotFound(new
            {
                code = "APPLICATION_NOT_FOUND",
                message = "No organizer application found for the current user."
            });
        }

        return Ok(application);
    }

    /// <summary>
    /// PUT /api/organizer-applications/me/resubmit
    /// Resubmits a previously rejected application for the authenticated customer.
    /// Updates the existing application and transitions its status back to Pending.
    /// </summary>
    [HttpPut("me/resubmit")]
    [HttpPost("me/resubmit")]
    public async Task<IActionResult> ResubmitApplication(
        [FromBody] CreateOrganizerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new
            {
                code = "VALIDATION_ERROR",
                message = "Validation failed.",
                errors
            });
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = "Invalid or missing user identity in authentication token."
            });
        }

        var (result, error, statusCode, errorCode) = await _applicationService.ResubmitApplicationAsync(
            userId, request, cancellationToken);

        if (error is not null)
        {
            return StatusCode(statusCode, new
            {
                code = errorCode ?? "APPLICATION_ERROR",
                message = error
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/organizer-applications (or /api/organizer-applications/admin/pending)
    /// Retrieves organizer applications for review by an Administrator.
    /// </summary>
    [HttpGet("admin")]
    [HttpGet("admin/pending")]
    [HttpGet("/api/admin/organizer-applications")]
    [Authorize(Policy = "AdministratorOnly")]
    public async Task<IActionResult> GetAdminApplications(
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        OrganizerApplicationStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<OrganizerApplicationStatus>(status, ignoreCase: true, out var parsed))
        {
            statusFilter = parsed;
        }
        else if (Request.Path.Value?.EndsWith("/pending", StringComparison.OrdinalIgnoreCase) == true || string.IsNullOrWhiteSpace(status))
        {
            statusFilter = OrganizerApplicationStatus.Pending;
        }

        var list = await _applicationService.GetAdminApplicationsAsync(statusFilter, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// GET /api/admin/organizer-applications/{id}
    /// Retrieves a single organizer application for review by an Administrator.
    /// </summary>
    [HttpGet("admin/{id:guid}")]
    [HttpGet("/api/admin/organizer-applications/{id:guid}")]
    [Authorize(Policy = "AdministratorOnly")]
    public async Task<IActionResult> GetAdminApplicationById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var application = await _applicationService.GetAdminApplicationByIdAsync(id, cancellationToken);
        if (application is null)
        {
            return NotFound(new
            {
                code = "APPLICATION_NOT_FOUND",
                message = "Organizer application not found."
            });
        }

        return Ok(application);
    }

    /// <summary>
    /// POST /api/admin/organizer-applications/{id}/approve
    /// Approves an application and atomically grants the Organizer role to the user.
    /// </summary>
    [HttpPost("admin/{id:guid}/approve")]
    [HttpPost("/api/admin/organizer-applications/{id:guid}/approve")]
    [Authorize(Policy = "AdministratorOnly")]
    public async Task<IActionResult> ApproveApplication(
        [FromRoute] Guid id,
        [FromBody] ApproveOrganizerApplicationRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = "Invalid or missing reviewer identity in authentication token."
            });
        }

        var (result, error, statusCode, errorCode) = await _applicationService.ApproveApplicationAsync(
            id, adminId, request, cancellationToken);

        if (error is not null)
        {
            return StatusCode(statusCode, new
            {
                code = errorCode ?? "APPROVAL_ERROR",
                message = error
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// POST /api/admin/organizer-applications/{id}/reject
    /// Rejects an application with mandatory reviewer feedback.
    /// </summary>
    [HttpPost("admin/{id:guid}/reject")]
    [HttpPost("/api/admin/organizer-applications/{id:guid}/reject")]
    [Authorize(Policy = "AdministratorOnly")]
    public async Task<IActionResult> RejectApplication(
        [FromRoute] Guid id,
        [FromBody] RejectOrganizerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(new
            {
                code = "VALIDATION_ERROR",
                message = "Validation failed.",
                errors
            });
        }

        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = "Invalid or missing reviewer identity in authentication token."
            });
        }

        var (result, error, statusCode, errorCode) = await _applicationService.RejectApplicationAsync(
            id, adminId, request, cancellationToken);

        if (error is not null)
        {
            return StatusCode(statusCode, new
            {
                code = errorCode ?? "REJECTION_ERROR",
                message = error
            });
        }

        return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(idStr, out userId);
    }
}
