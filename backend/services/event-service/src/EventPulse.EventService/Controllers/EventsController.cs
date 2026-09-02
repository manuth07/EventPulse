using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly EventDbContext _context;
    private readonly ILogger<EventsController> _logger;

    public EventsController(EventDbContext context, ILogger<EventsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =========================================================================
    // PUBLIC — Anonymous access
    // =========================================================================

    /// <summary>
    /// GET /api/events
    /// Public. Returns all events (EP-103 foundation; filtering added in EP-104).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventListDto>>> GetEvents()
    {
        var events = await _context.Events
            .AsNoTracking()
            .Select(e => new EventListDto
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                Venue = e.Venue,
                EventDate = e.EventDate,
                Price = e.Price,
                Status = e.Status
            })
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// GET /api/events/{id}
    /// Public. Returns a single Published or Approved event.
    /// Returns 404 for non-existent, non-public, or invalid events.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<EventDetailsDto>> GetEventById(string id)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound();

        var eventItem = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == guidId);

        if (eventItem == null)
            return NotFound();

        // Only Published or Approved events are visible to public visitors
        if (eventItem.Status != EventStatus.Published && eventItem.Status != EventStatus.Approved)
            return NotFound();

        var details = new EventDetailsDto
        {
            Id = eventItem.Id,
            Title = eventItem.Title,
            Description = eventItem.Description,
            Venue = eventItem.Venue,
            EventDate = eventItem.EventDate,
            Price = eventItem.Price,
            OrganizerId = eventItem.OrganizerId
        };

        return Ok(details);
    }

    // =========================================================================
    // EP-96 — ORGANIZER ENDPOINTS
    // Require: Organizer role (OrganizerOnly policy)
    // No JWT → 401. Valid JWT, wrong role → 403.
    // =========================================================================

    /// <summary>
    /// POST /api/events
    /// EP-96 — Organizer submits a new event for review.
    /// The submitting Organizer's identity is derived from the authenticated JWT (sub claim).
    /// Requires: OrganizerOnly policy (Organizer role).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<EventDetailsDto>> SubmitEvent([FromBody] SubmitEventRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { code = "INVALID_REQUEST", message = "Validation failed.", errors });
        }

        // Derive organizer identity from validated JWT — never from request body
        var organizerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(organizerIdStr, out var organizerId))
        {
            _logger.LogWarning("SubmitEvent: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (request.EventDate <= DateTime.UtcNow)
        {
            return BadRequest(new { code = "INVALID_DATE", message = "Event date must be in the future." });
        }

        var newEvent = new Event
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Venue = request.Venue.Trim(),
            EventDate = request.EventDate,
            Price = request.Price,
            Status = EventStatus.Pending,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Event submitted. EventId={EventId}, OrganizerId={OrganizerId}, Title={Title}",
            newEvent.Id, organizerId, newEvent.Title);

        var responseDto = new EventDetailsDto
        {
            Id = newEvent.Id,
            Title = newEvent.Title,
            Description = newEvent.Description,
            Venue = newEvent.Venue,
            EventDate = newEvent.EventDate,
            Price = newEvent.Price,
            OrganizerId = newEvent.OrganizerId,
        };

        return CreatedAtAction(nameof(GetEventById), new { id = newEvent.Id.ToString() }, responseDto);
    }

    // =========================================================================
    // EP-97 — ADMINISTRATOR ENDPOINTS
    // Require: Administrator role (AdministratorOnly policy)
    // No JWT → 401. Valid JWT, wrong role (Customer / Organizer) → 403.
    // =========================================================================

    /// <summary>
    /// PUT /api/events/{id}/approve
    /// EP-97 — Administrator approves a Pending event submission.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpPut("{id}/approve")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> ApproveEvent(string id, [FromBody] ReviewEventRequest request)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound();

        var eventItem = await _context.Events.FindAsync(guidId);
        if (eventItem == null)
            return NotFound();

        if (eventItem.Status != EventStatus.Pending)
        {
            return Conflict(new
            {
                code = "INVALID_STATE",
                message = $"Only Pending events can be approved. Current status: {eventItem.Status}."
            });
        }

        var reviewerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(reviewerIdStr, out var reviewerId);

        eventItem.Status = EventStatus.Approved;
        eventItem.ReviewedAt = DateTime.UtcNow;
        eventItem.ReviewedBy = reviewerId;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Event approved. EventId={EventId}, ReviewedBy={ReviewerId}",
            guidId, reviewerId);

        return Ok(new { message = "Event approved successfully.", eventId = guidId, status = "Approved" });
    }

    /// <summary>
    /// PUT /api/events/{id}/reject
    /// EP-97 — Administrator rejects a Pending event submission.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpPut("{id}/reject")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> RejectEvent(string id, [FromBody] ReviewEventRequest request)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound();

        var eventItem = await _context.Events.FindAsync(guidId);
        if (eventItem == null)
            return NotFound();

        if (eventItem.Status != EventStatus.Pending)
        {
            return Conflict(new
            {
                code = "INVALID_STATE",
                message = $"Only Pending events can be rejected. Current status: {eventItem.Status}."
            });
        }

        var reviewerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(reviewerIdStr, out var reviewerId);

        eventItem.Status = EventStatus.Rejected;
        eventItem.ReviewedAt = DateTime.UtcNow;
        eventItem.ReviewedBy = reviewerId;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Event rejected. EventId={EventId}, ReviewedBy={ReviewerId}",
            guidId, reviewerId);

        return Ok(new { message = "Event rejected.", eventId = guidId, status = "Rejected" });
    }

    /// <summary>
    /// PUT /api/events/{id}/publish
    /// EP-97 — Administrator publishes an Approved event, making it visible to the public.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpPut("{id}/publish")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> PublishEvent(string id)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound();

        var eventItem = await _context.Events.FindAsync(guidId);
        if (eventItem == null)
            return NotFound();

        if (eventItem.Status != EventStatus.Approved)
        {
            return Conflict(new
            {
                code = "INVALID_STATE",
                message = $"Only Approved events can be published. Current status: {eventItem.Status}."
            });
        }

        var publisherIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(publisherIdStr, out var publisherId);

        eventItem.Status = EventStatus.Published;
        // ReviewedBy/ReviewedAt already set at approve time; preserve them

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Event published. EventId={EventId}, PublishedBy={PublisherId}",
            guidId, publisherId);

        return Ok(new { message = "Event published successfully.", eventId = guidId, status = "Published" });
    }
}
