using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using EventPulse.EventService.Storage;

namespace EventPulse.EventService.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly EventDbContext _context;
    private readonly IEventSubmissionService? _submissionService;
    private readonly IEventReviewService? _reviewService;
    private readonly IEventImageStorage? _imageStorage;
    private readonly ILogger<EventsController>? _logger;

    public EventsController(
        EventDbContext context,
        IEventSubmissionService? submissionService = null,
        IEventReviewService? reviewService = null,
        IEventImageStorage? imageStorage = null,
        ILogger<EventsController>? logger = null)
    {
        _context = context;
        _submissionService = submissionService;
        _reviewService = reviewService;
        _imageStorage = imageStorage;
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
        // Only Published or Approved events are visible to public visitors.
        // Pending and Rejected events are never exposed here.
        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Published || e.Status == EventStatus.Approved)
            .ToListAsync();

        var dtos = events.Select(e => new EventListDto
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            Venue = e.Venue,
            EventDate = e.EventDate,
            Price = e.Price,
            Status = e.Status,
            ImageUrl = _imageStorage?.GetPublicUrl(e.ImageBlobName)
        }).ToList();

        return Ok(dtos);
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
            OrganizerId = eventItem.OrganizerId,
            ImageUrl = _imageStorage?.GetPublicUrl(eventItem.ImageBlobName)
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
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB ceiling — covers 5 MB image + form fields
    public async Task<ActionResult<EventSubmissionResponseDto>> SubmitEvent(
        [FromForm] CreateEventRequest request,
        CancellationToken cancellationToken)
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
            _logger?.LogWarning("SubmitEvent: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_submissionService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Submission service is not configured." });

        var (result, error) = await _submissionService.CreateAsync(request, organizerId, cancellationToken);

        if (result is null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });

        return CreatedAtAction(nameof(GetEventById), new { id = result.Id.ToString() }, result);
    }

    /// <summary>
    /// GET /api/events/my-submissions
    /// EP-30 — Returns all event submissions created by the authenticated Organizer, ordered newest first.
    /// Exposes events across all lifecycle statuses (Pending, Approved, Rejected, Published).
    /// Requires: OrganizerOnly policy (Organizer role).
    /// </summary>
    [HttpGet("my-submissions")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<IReadOnlyList<OrganizerEventSubmissionDto>>> GetMySubmissions(
        CancellationToken cancellationToken)
    {
        // Derive organizer identity from validated JWT — never from request query or body
        var organizerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(organizerIdStr, out var organizerId))
        {
            _logger?.LogWarning("GetMySubmissions: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_submissionService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Submission service is not configured." });

        var submissions = await _submissionService.GetOrganizerSubmissionsAsync(organizerId, cancellationToken);
        return Ok(submissions);
    }

    // =========================================================================
    // EP-31 / EP-97 — ADMINISTRATOR REVIEW ENDPOINTS
    // Require: Administrator role (AdministratorOnly policy)
    // No JWT → 401. Valid JWT, wrong role (Customer / Organizer) → 403.
    // =========================================================================

    /// <summary>
    /// GET /api/events/admin/pending
    /// EP-31 — Retrieves all event submissions awaiting Administrator review.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpGet("admin/pending")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<IReadOnlyList<AdminEventReviewDto>>> GetPendingEvents(
        CancellationToken cancellationToken)
    {
        if (_reviewService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Review service is not configured." });

        var pendingEvents = await _reviewService.GetPendingEventsAsync(cancellationToken);
        return Ok(pendingEvents);
    }

    /// <summary>
    /// GET /api/events/admin/pending/{id}
    /// EP-31 — Retrieves a single Pending event submission by ID for Administrator review.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpGet("admin/pending/{id}")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<AdminEventReviewDto>> GetPendingEventById(
        string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound();

        if (_reviewService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Review service is not configured." });

        var eventDto = await _reviewService.GetPendingEventByIdAsync(guidId, cancellationToken);
        if (eventDto == null)
            return NotFound();

        return Ok(eventDto);
    }

    /// <summary>
    /// POST|PUT /api/events/{id}/approve
    /// EP-31 / EP-97 — Administrator approves a Pending event submission.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpPost("{id}/approve")]
    [HttpPut("{id}/approve")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> ApproveEvent(
        string id,
        [FromBody] ReviewEventRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound();

        if (_reviewService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Review service is not configured." });

        var reviewerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(reviewerIdStr, out var reviewerId);

        var (result, error, isNotFound) = await _reviewService.ApproveEventAsync(guidId, reviewerId, cancellationToken);

        if (isNotFound)
            return NotFound();

        if (error != null)
        {
            return Conflict(new
            {
                code = "INVALID_STATE",
                message = error
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// POST|PUT /api/events/{id}/reject
    /// EP-31 / EP-97 — Administrator rejects a Pending event submission.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpPost("{id}/reject")]
    [HttpPut("{id}/reject")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> RejectEvent(
        string id,
        [FromBody] ReviewEventRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound();

        if (_reviewService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Review service is not configured." });

        var reviewerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(reviewerIdStr, out var reviewerId);

        var (result, error, isNotFound) = await _reviewService.RejectEventAsync(guidId, reviewerId, request?.Notes, cancellationToken);

        if (isNotFound)
            return NotFound();

        if (error != null)
        {
            return Conflict(new
            {
                code = "INVALID_STATE",
                message = error
            });
        }

        return Ok(result);
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

        _logger?.LogInformation(
            "Event published. EventId={EventId}, PublishedBy={PublisherId}",
            guidId, publisherId);

        return Ok(new { message = "Event published successfully.", eventId = guidId, status = "Published" });
    }
}
