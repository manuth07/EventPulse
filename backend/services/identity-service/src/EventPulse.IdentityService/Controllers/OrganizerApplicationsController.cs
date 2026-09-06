using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.IdentityService.DTOs;
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

    private bool TryGetUserId(out Guid userId)
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(idStr, out userId);
    }
}
