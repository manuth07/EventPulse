using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Services;

namespace EventPulse.EventService.Controllers;

/// <summary>
/// EP-19 — Organizer endpoints for managing ticket types on an eligible event.
/// Requires: Organizer role (OrganizerOnly policy). No JWT → 401. Wrong role → 403.
/// </summary>
[ApiController]
[Route("api/events/{eventId}/ticket-types")]
public class TicketTypesController : ControllerBase
{
    private readonly ITicketTypeService? _ticketTypeService;
    private readonly ILogger<TicketTypesController>? _logger;

    public TicketTypesController(
        ITicketTypeService? ticketTypeService = null,
        ILogger<TicketTypesController>? logger = null)
    {
        _ticketTypeService = ticketTypeService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/events/{eventId}/ticket-types
    /// Creates a ticket type for an eligible (Approved/Published) event owned by the caller.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<IActionResult> CreateTicketType(
        string eventId,
        [FromBody] CreateTicketTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(eventId, out var guidId))
            return NotFound();

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        var organizerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(organizerIdStr, out var organizerId))
        {
            _logger?.LogWarning("CreateTicketType: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_ticketTypeService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Ticket type service is not configured." });

        var (result, error, isNotFound, isForbidden, isInvalidState) =
            await _ticketTypeService.CreateAsync(guidId, request, organizerId, cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = error });

        if (isForbidden)
            return StatusCode(403, new { code = "FORBIDDEN", message = error });

        if (isInvalidState)
            return Conflict(new { code = "INVALID_STATE", message = error });

        if (error != null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });

        return CreatedAtAction(nameof(GetTicketTypes), new { eventId = guidId }, result);
    }

    /// <summary>
    /// GET /api/events/{eventId}/ticket-types
    /// Returns all ticket types configured for the event, for the owning organizer.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<IActionResult> GetTicketTypes(string eventId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(eventId, out var guidId))
            return NotFound();

        var organizerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(organizerIdStr, out var organizerId))
        {
            _logger?.LogWarning("GetTicketTypes: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_ticketTypeService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Ticket type service is not configured." });

        var (result, isNotFound, isForbidden) =
            await _ticketTypeService.GetByEventIdAsync(guidId, organizerId, cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = "Event not found." });

        if (isForbidden)
            return StatusCode(403, new { code = "FORBIDDEN", message = "You do not have permission to view ticket types for this event." });

        return Ok(result);
    }
        /// <summary>
    /// PUT /api/events/{eventId}/ticket-types/{ticketTypeId}
    /// Updates an existing ticket type's name, price, and capacity.
    /// Only the owning Organizer may update; EventId association cannot be changed.
    /// </summary>
    [HttpPut("{ticketTypeId}")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<IActionResult> UpdateTicketType(
        string eventId,
        string ticketTypeId,
        [FromBody] UpdateTicketTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(eventId, out var eventGuidId) || !Guid.TryParse(ticketTypeId, out var ticketTypeGuidId))
            return NotFound();

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        var organizerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(organizerIdStr, out var organizerId))
        {
            _logger?.LogWarning("UpdateTicketType: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_ticketTypeService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Ticket type service is not configured." });

        var (result, error, isNotFound, isForbidden) =
            await _ticketTypeService.UpdateAsync(eventGuidId, ticketTypeGuidId, request, organizerId, cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = error });

        if (isForbidden)
            return StatusCode(403, new { code = "FORBIDDEN", message = error });

        if (error != null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });

        return Ok(result);
    }
}