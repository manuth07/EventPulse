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
    private readonly IEventUpdateRequestService? _updateRequestService;
    private readonly IEventCancellationRequestService? _cancellationRequestService;
    private readonly IEventImageStorage? _imageStorage;
    private readonly ILogger<EventsController>? _logger;

    public EventsController(
        EventDbContext context,
        IEventSubmissionService? submissionService = null,
        IEventReviewService? reviewService = null,
        IEventUpdateRequestService? updateRequestService = null,
        IEventCancellationRequestService? cancellationRequestService = null,
        IEventImageStorage? imageStorage = null,
        ILogger<EventsController>? logger = null)
    {
        _context = context;
        _submissionService = submissionService;
        _reviewService = reviewService;
        _updateRequestService = updateRequestService;
        _cancellationRequestService = cancellationRequestService;
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
            Category = e.Category,
            VenueType = e.VenueType,
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
            Category = eventItem.Category,
            VenueType = eventItem.VenueType,
            OrganizerId = eventItem.OrganizerId,
            ImageUrl = _imageStorage?.GetPublicUrl(eventItem.ImageBlobName),
            CoverUrl = _imageStorage?.GetPublicUrl(eventItem.CoverBlobName)
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
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB ceiling — covers 2x 5 MB images (poster + cover) + form fields
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

    /// <summary>
    /// GET /api/events/my-submissions/{id}
    /// Retrieves a single event submission belonging to the authenticated Organizer.
    /// Exposes review comments and status for Organizer inspection.
    /// Requires: OrganizerOnly policy (Organizer role).
    /// </summary>
    [HttpGet("my-submissions/{id}")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<OrganizerEventSubmissionDto>> GetMySubmissionById(
        string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound();

        var organizerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(organizerIdStr, out var organizerId))
        {
            _logger?.LogWarning("GetMySubmissionById: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_submissionService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Submission service is not configured." });

        var submission = await _submissionService.GetOrganizerSubmissionByIdAsync(guidId, organizerId, cancellationToken);
        if (submission == null)
            return NotFound();

        return Ok(submission);
    }

    /// <summary>
    /// PUT /api/events/{id}/resubmit
    /// Edits and resubmits a Rejected event submission for Administrator review.
    /// Transitions status: Rejected -> Pending.
    /// Preserves existing Event ID and previous review notes.
    /// Requires: OrganizerOnly policy (Organizer role).
    /// </summary>
    [HttpPut("{id}/resubmit")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> ResubmitEvent(
        string id,
        [FromForm] ResubmitEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guidId))
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
            _logger?.LogWarning("ResubmitEvent: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_submissionService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Submission service is not configured." });

        var (result, error, isNotFound, isForbidden, isInvalidState) =
            await _submissionService.ResubmitAsync(guidId, request, organizerId, cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = error });

        if (isForbidden)
            return StatusCode(403, new { code = "FORBIDDEN", message = error });

        if (isInvalidState)
            return Conflict(new { code = "INVALID_STATE", message = error });

        if (error != null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });

        return Ok(result);
    }

    /// <summary>
    /// POST /api/events/{id}/update-request
    /// EP-34 / US-14 — Organizer submits an update request for an Approved or Published event.
    /// Does NOT modify the live event record. Changes are stored in Pending status for Admin review.
    /// Exactly one Pending update request is allowed at a time.
    /// Requires: OrganizerOnly policy.
    /// </summary>
    [HttpPost("{id}/update-request")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> SubmitUpdateRequest(
        string id,
        [FromForm] SubmitEventUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound(new { code = "NOT_FOUND", message = "Event not found." });

        var organizerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(organizerIdStr, out var organizerId))
        {
            _logger?.LogWarning("SubmitUpdateRequest: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_updateRequestService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Update request service is not configured." });

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await _updateRequestService.SubmitUpdateRequestAsync(guidId, request, organizerId, cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = error });

        if (isForbidden)
            return StatusCode(403, new { code = "FORBIDDEN", message = error });

        if (isInvalidState)
            return Conflict(new { code = "INVALID_STATE", message = error });

        if (isConflict)
            return Conflict(new { code = "CONFLICT", message = error });

        if (error != null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });

        return CreatedAtAction(nameof(GetUpdateRequest), new { id = guidId.ToString() }, result);
    }

    /// <summary>
    /// GET /api/events/{id}/update-request
    /// EP-34 / US-14 — Retrieves the latest update request for an event owned by the authenticated Organizer.
    /// Includes change detection flags (HasVenueChanged, HasDateChanged, IsMajorChange).
    /// Requires: OrganizerOnly policy.
    /// </summary>
    [HttpGet("{id}/update-request")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<EventUpdateRequestDto>> GetUpdateRequest(
        string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound(new { code = "NOT_FOUND", message = "Event not found." });

        var organizerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(organizerIdStr, out var organizerId))
        {
            _logger?.LogWarning("GetUpdateRequest: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_updateRequestService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Update request service is not configured." });

        var (result, error, isNotFound, isForbidden) =
            await _updateRequestService.GetUpdateRequestAsync(guidId, organizerId, cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = error });

        if (isForbidden)
            return StatusCode(403, new { code = "FORBIDDEN", message = error });

        return Ok(result);
    }

    /// <summary>
    /// POST /api/events/{id}/cancellation-request
    /// EP-35 / US-15 — Organizer submits a cancellation request for an Approved or Published event.
    /// Does NOT modify the live event status. Creates an EventCancellationRequest in Pending status.
    /// Exactly one Pending cancellation request is allowed at a time.
    /// Mutually exclusive with pending update requests.
    /// Requires: OrganizerOnly policy.
    /// </summary>
    [HttpPost("{id}/cancellation-request")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<IActionResult> SubmitCancellationRequest(
        string id,
        [FromBody] SubmitEventCancellationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound(new { code = "NOT_FOUND", message = "Event not found." });

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
            _logger?.LogWarning("SubmitCancellationRequest: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_cancellationRequestService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Cancellation request service is not configured." });

        var (result, error, isNotFound, isForbidden, isInvalidState, isConflict) =
            await _cancellationRequestService.SubmitCancellationRequestAsync(guidId, request, organizerId, cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = error });

        if (isForbidden)
            return StatusCode(403, new { code = "FORBIDDEN", message = error });

        if (isInvalidState)
            return Conflict(new { code = "INVALID_STATE", message = error });

        if (isConflict)
            return Conflict(new { code = "CONFLICT", message = error });

        if (error != null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });

        return CreatedAtAction(nameof(GetCancellationRequest), new { id = guidId.ToString() }, result);
    }

    /// <summary>
    /// GET /api/events/{id}/cancellation-request
    /// EP-35 / US-15 — Retrieves the latest cancellation request for an event owned by the authenticated Organizer.
    /// Requires: OrganizerOnly policy.
    /// </summary>
    [HttpGet("{id}/cancellation-request")]
    [Authorize(Policy = AppPolicies.OrganizerOnly)]
    public async Task<ActionResult<EventCancellationRequestDto>> GetCancellationRequest(
        string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound(new { code = "NOT_FOUND", message = "Event not found." });

        var organizerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(organizerIdStr, out var organizerId))
        {
            _logger?.LogWarning("GetCancellationRequest: Could not parse OrganizerId from JWT sub claim.");
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Invalid token identity." });
        }

        if (_cancellationRequestService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Cancellation request service is not configured." });

        var (result, error, isNotFound, isForbidden) =
            await _cancellationRequestService.GetCancellationRequestAsync(guidId, organizerId, cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = error });

        if (isForbidden)
            return StatusCode(403, new { code = "FORBIDDEN", message = error });

        return Ok(result);
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

        if (string.IsNullOrWhiteSpace(request?.Notes))
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Rejection feedback is required." });
        }

        if (_reviewService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Review service is not configured." });

        var reviewerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(reviewerIdStr, out var reviewerId);

        var (result, error, isNotFound) = await _reviewService.RejectEventAsync(guidId, reviewerId, request.Notes.Trim(), cancellationToken);

        if (isNotFound)
            return NotFound();

        if (error != null)
        {
            if (error == "Rejection feedback is required.")
            {
                return BadRequest(new { code = "VALIDATION_ERROR", message = error });
            }

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

    // =========================================================================
    // EP-210 / US-14 — ADMINISTRATOR EVENT UPDATE REQUEST REVIEW ENDPOINTS
    // Require: Administrator role (AdministratorOnly policy)
    // No JWT -> 401. Valid JWT, non-admin -> 403.
    // =========================================================================

    /// <summary>
    /// GET /api/events/admin/update-requests/pending
    /// EP-210 / US-14 — Retrieves all Event Update Requests awaiting Administrator review (Status = Pending).
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpGet("admin/update-requests/pending")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<IReadOnlyList<AdminEventUpdateComparisonDto>>> GetPendingUpdateRequests(
        CancellationToken cancellationToken)
    {
        if (_updateRequestService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Update request service is not configured." });

        var requests = await _updateRequestService.GetPendingUpdateRequestsAsync(cancellationToken);
        return Ok(requests);
    }

    /// <summary>
    /// GET /api/events/admin/update-requests/{id}
    /// EP-210 / US-14 — Retrieves a single Event Update Request with side-by-side comparison for Admin review.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpGet("admin/update-requests/{id}")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<AdminEventUpdateComparisonDto>> GetUpdateRequestReview(
        string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound(new { code = "NOT_FOUND", message = "Update request not found." });

        if (_updateRequestService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Update request service is not configured." });

        var comparison = await _updateRequestService.GetUpdateRequestReviewAsync(guidId, cancellationToken);
        if (comparison == null)
            return NotFound(new { code = "NOT_FOUND", message = "Update request not found." });

        return Ok(comparison);
    }

    /// <summary>
    /// POST|PUT /api/events/admin/update-requests/{id}/approve
    /// EP-210 / US-14 — Administrator approves an Event Update Request.
    /// Atomically applies proposed changes to the live Event record, updates request status to Approved,
    /// and records reviewer metadata.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpPost("admin/update-requests/{id}/approve")]
    [HttpPut("admin/update-requests/{id}/approve")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> ApproveUpdateRequest(
        string id,
        [FromBody] ReviewEventRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound(new { code = "NOT_FOUND", message = "Update request not found." });

        if (_updateRequestService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Update request service is not configured." });

        var reviewerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(reviewerIdStr, out var reviewerId);

        var (result, error, isNotFound, isInvalidState) = await _updateRequestService.ApproveUpdateRequestAsync(
            guidId, reviewerId, request?.Notes, cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = error });

        if (isInvalidState)
            return Conflict(new { code = "INVALID_STATE", message = error });

        if (error != null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });

        return Ok(result);
    }

    /// <summary>
    /// POST|PUT /api/events/admin/update-requests/{id}/reject
    /// EP-210 / US-14 — Administrator rejects an Event Update Request.
    /// Requires mandatory rejection feedback/notes. Leaves live Event unchanged, sets status to Rejected.
    /// Requires: AdministratorOnly policy.
    /// </summary>
    [HttpPost("admin/update-requests/{id}/reject")]
    [HttpPut("admin/update-requests/{id}/reject")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> RejectUpdateRequest(
        string id,
        [FromBody] ReviewEventRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guidId))
            return NotFound(new { code = "NOT_FOUND", message = "Update request not found." });

        if (string.IsNullOrWhiteSpace(request?.Notes))
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Rejection feedback is required." });
        }

        if (_updateRequestService is null)
            return StatusCode(500, new { code = "SERVICE_UNAVAILABLE", message = "Update request service is not configured." });

        var reviewerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")?.Value;
        Guid.TryParse(reviewerIdStr, out var reviewerId);

        var (result, error, isNotFound, isInvalidState) = await _updateRequestService.RejectUpdateRequestAsync(
            guidId, reviewerId, request.Notes.Trim(), cancellationToken);

        if (isNotFound)
            return NotFound(new { code = "NOT_FOUND", message = error });

        if (isInvalidState)
            return Conflict(new { code = "INVALID_STATE", message = error });

        if (error != null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = error });

        return Ok(result);
    }
}
