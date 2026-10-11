using System.Security.Claims;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventPulse.BookingService.Controllers;

[ApiController]
[Route("api/organizer/analytics/bookings")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class OrganizerBookingAnalyticsController : ControllerBase
{
    private readonly IOrganizerBookingAnalyticsService _analyticsService;
    private readonly ILogger<OrganizerBookingAnalyticsController> _logger;

    public OrganizerBookingAnalyticsController(
        IOrganizerBookingAnalyticsService analyticsService,
        ILogger<OrganizerBookingAnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    private bool TryGetOrganizerId(out Guid organizerId)
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(idStr, out organizerId);
    }

    /// <summary>
    /// GET /api/organizer/analytics/bookings
    /// EP-389: Retrieves overall booking metrics for the authenticated organizer's events.
    /// Returns 0s if organizer has no bookings. Supports optional date filtering.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<OrganizerBookingSummaryDto>> GetOrganizerBookingSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        if (!TryGetOrganizerId(out var organizerId))
            return Unauthorized(new { message = "Invalid or missing organizer identity." });

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest(new { message = "'from' date cannot be after 'to' date." });

        var summary = await _analyticsService.GetOrganizerBookingSummaryAsync(organizerId, from, to, ct);
        return Ok(summary);
    }

    /// <summary>
    /// GET /api/organizer/analytics/bookings/events
    /// EP-389: Retrieves paginated event-level booking summaries for all events owned by the organizer.
    /// Supports search by event title, date filtering, and pagination.
    /// </summary>
    [HttpGet("events")]
    public async Task<ActionResult<PagedResult<EventBookingSummaryItemDto>>> GetOrganizerEventsBookingList(
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!TryGetOrganizerId(out var organizerId))
            return Unauthorized(new { message = "Invalid or missing organizer identity." });

        if (page < 1)
            return BadRequest(new { message = "Page must be greater than or equal to 1." });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "PageSize must be between 1 and 100." });

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest(new { message = "'from' date cannot be after 'to' date." });

        var result = await _analyticsService.GetOrganizerEventsBookingListAsync(organizerId, search, from, to, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/organizer/analytics/bookings/events/{eventId}
    /// EP-389: Retrieves booking analytics for a specific event owned by the authenticated organizer,
    /// including breakdown by ticket type, cancellation rate, and remaining capacity.
    /// Enforces event ownership verification. Returns 404 if event does not exist or belongs to another organizer.
    /// </summary>
    [HttpGet("events/{eventId:guid}")]
    public async Task<ActionResult<EventBookingAnalyticsDto>> GetEventBookingAnalytics(
        Guid eventId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        if (!TryGetOrganizerId(out var organizerId))
            return Unauthorized(new { message = "Invalid or missing organizer identity." });

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest(new { message = "'from' date cannot be after 'to' date." });

        var analytics = await _analyticsService.GetEventBookingAnalyticsAsync(organizerId, eventId, from, to, ct);
        if (analytics == null)
        {
            return NotFound(new { message = $"Event {eventId} was not found or is not accessible." });
        }

        return Ok(analytics);
    }
}
