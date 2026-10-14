using System.Security.Claims;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventPulse.BookingService.Controllers;

[ApiController]
[Route("api/organizer/analytics/sales")]
[Authorize(Policy = AppPolicies.OrganizerOnly)]
public class OrganizerSalesAnalyticsController : ControllerBase
{
    private readonly IOrganizerSalesAnalyticsService _analyticsService;
    private readonly ILogger<OrganizerSalesAnalyticsController> _logger;

    public OrganizerSalesAnalyticsController(
        IOrganizerSalesAnalyticsService analyticsService,
        ILogger<OrganizerSalesAnalyticsController> logger)
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
    /// GET /api/organizer/analytics/sales
    /// EP-384 / EP-395: Retrieves overall sales metrics for the authenticated organizer's events.
    /// Returns 0s if organizer has no sales. Supports optional date filtering and server-side validation.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<OrganizerSalesSummaryDto>> GetOrganizerSalesSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var authCheck = ValidateOrganizerAuthorization(out var organizerId);
        if (!authCheck.IsValid)
            return authCheck.ErrorResult!;

        var filterValidation = ValidateFilters(from, to, eventId: null);
        if (!filterValidation.IsValid)
            return filterValidation.ErrorResult!;

        var summary = await _analyticsService.GetOrganizerSalesSummaryAsync(organizerId, from, to, ct);
        return Ok(summary);
    }

    /// <summary>
    /// GET /api/organizer/analytics/sales/events
    /// EP-384 / EP-395: Retrieves paginated event-level sales summaries for all events owned by the organizer.
    /// Supports search by event title, date filtering, pagination, and server-side validation.
    /// </summary>
    [HttpGet("events")]
    public async Task<ActionResult<PagedResult<EventSalesSummaryItemDto>>> GetOrganizerEventsSalesList(
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var authCheck = ValidateOrganizerAuthorization(out var organizerId);
        if (!authCheck.IsValid)
            return authCheck.ErrorResult!;

        if (page < 1)
            return BadRequest(new { code = "INVALID_PAGE", message = "Page must be greater than or equal to 1." });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { code = "INVALID_PAGE_SIZE", message = "PageSize must be between 1 and 100." });

        var filterValidation = ValidateFilters(from, to, eventId: null);
        if (!filterValidation.IsValid)
            return filterValidation.ErrorResult!;

        var result = await _analyticsService.GetOrganizerEventsSalesListAsync(organizerId, search, from, to, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/organizer/analytics/sales/events/{eventId}
    /// EP-384 / EP-395: Retrieves sales analytics for a specific event owned by the authenticated organizer,
    /// including breakdown by ticket type. Enforces event ownership verification.
    /// Returns 404 if event does not exist or belongs to another organizer.
    /// </summary>
    [HttpGet("events/{eventId:guid}")]
    public async Task<ActionResult<EventSalesAnalyticsDto>> GetEventSalesAnalytics(
        Guid eventId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var authCheck = ValidateOrganizerAuthorization(out var organizerId);
        if (!authCheck.IsValid)
            return authCheck.ErrorResult!;

        var filterValidation = ValidateFilters(from, to, eventId);
        if (!filterValidation.IsValid)
            return filterValidation.ErrorResult!;

        var analytics = await _analyticsService.GetEventSalesAnalyticsAsync(organizerId, eventId, from, to, ct);
        if (analytics == null)
        {
            return NotFound(new
            {
                code = "EVENT_NOT_FOUND",
                message = $"Event {eventId} was not found or is not accessible."
            });
        }

        return Ok(analytics);
    }

    private (bool IsValid, ActionResult? ErrorResult) ValidateOrganizerAuthorization(out Guid organizerId)
    {
        organizerId = Guid.Empty;

        // Prohibit client-supplied organizerId filter for organizers (EP-393 Section 4.2 & 7.1)
        if (Request?.Query?.ContainsKey("organizerId") == true)
        {
            return (false, StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "UNAUTHORIZED_FILTER",
                message = "You do not have permission to filter by organizer."
            }));
        }

        if (!TryGetOrganizerId(out organizerId))
        {
            return (false, Unauthorized(new
            {
                code = "UNAUTHORIZED",
                message = "Invalid or missing organizer identity."
            }));
        }

        return (true, null);
    }

    private (bool IsValid, ActionResult? ErrorResult) ValidateFilters(DateTimeOffset? from, DateTimeOffset? to, Guid? eventId)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return (false, BadRequest(new
            {
                code = "INVALID_DATE_RANGE",
                message = "The start date ('from') must be earlier than or equal to the end date ('to')."
            }));
        }

        if (from.HasValue && to.HasValue && (to.Value - from.Value).TotalDays > 730)
        {
            return (false, BadRequest(new
            {
                code = "DATE_RANGE_EXCEEDED",
                message = "The requested date range exceeds the maximum allowed reporting period of 730 days (2 years)."
            }));
        }

        if (Request?.Query != null)
        {
            if (Request.Query.TryGetValue("category", out var categoryVal) &&
                !string.IsNullOrWhiteSpace(categoryVal) &&
                !categoryVal.ToString().Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (!ReportCategories.IsValid(categoryVal))
                {
                    return (false, BadRequest(new
                    {
                        code = "INVALID_CATEGORY",
                        message = $"Unsupported event category '{categoryVal}'. Valid categories are Music, Sports, Conference, Workshop, Festival, Arts & Theatre, Community, Other."
                    }));
                }
            }

            if (Request.Query.ContainsKey("ticketTypeId") && !eventId.HasValue)
            {
                return (false, BadRequest(new
                {
                    code = "MISSING_EVENT_FOR_TIER",
                    message = "An event must be selected when filtering by ticket type."
                }));
            }
        }

        return (true, null);
    }
}
