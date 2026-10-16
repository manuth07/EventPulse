using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;

namespace EventPulse.BookingService.Controllers;

[ApiController]
[Route("api/bookings/sales-reports")]
[Authorize(Policy = AppPolicies.AdminOnly)]
public class SalesReportsController : ControllerBase
{
    private readonly ISalesReportingService _salesReportingService;
    private readonly ILogger<SalesReportsController> _logger;

    public SalesReportsController(
        ISalesReportingService salesReportingService,
        ILogger<SalesReportsController> logger)
    {
        _salesReportingService = salesReportingService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/bookings/sales-reports
    /// EP-405: Retrieves a paginated collection of sales reports with optional filtering.
    /// Strictly protected by AdminOnly policy.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SalesReportDto>>> GetAll(
        [FromQuery] ReportFilterDto filter,
        CancellationToken ct)
    {
        filter ??= new ReportFilterDto();

        if (filter.Page < 1)
        {
            return BadRequest(new { code = "INVALID_PAGE", message = "Page must be greater than or equal to 1." });
        }

        if (filter.PageSize < 1 || filter.PageSize > 100)
        {
            return BadRequest(new { code = "INVALID_PAGE_SIZE", message = "PageSize must be between 1 and 100." });
        }

        if (filter.From.HasValue && filter.To.HasValue && filter.From.Value > filter.To.Value)
        {
            return BadRequest(new
            {
                code = "INVALID_DATE_RANGE",
                message = "The start date ('from') must be earlier than or equal to the end date ('to')."
            });
        }

        if (filter.From.HasValue && filter.To.HasValue && (filter.To.Value - filter.From.Value).TotalDays > 730)
        {
            return BadRequest(new
            {
                code = "DATE_RANGE_EXCEEDED",
                message = "The requested date range exceeds the maximum allowed reporting period of 730 days (2 years)."
            });
        }

        if (!string.IsNullOrWhiteSpace(filter.Category) && !filter.Category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (!ReportCategories.IsValid(filter.Category))
            {
                return BadRequest(new
                {
                    code = "INVALID_CATEGORY",
                    message = $"Unsupported event category '{filter.Category}'. Valid categories are Music, Sports, Conference, Workshop, Festival, Arts & Theatre, Community, Other."
                });
            }
        }

        if (filter.TicketTypeId.HasValue && !filter.EventId.HasValue)
        {
            return BadRequest(new
            {
                code = "MISSING_EVENT_FOR_TIER",
                message = "An event must be selected when filtering by ticket type."
            });
        }

        var result = await _salesReportingService.GetSalesReportsAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/bookings/sales-reports/{id}
    /// Retrieves a specific sales report by unique sale/transaction ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SalesReportDto>> GetById(Guid id, CancellationToken ct)
    {
        var report = await _salesReportingService.GetSalesReportByIdAsync(id, ct);
        if (report == null)
            return NotFound(new { message = $"Sales report with ID {id} not found" });

        return Ok(SalesReportDto.FromEntity(report));
    }

    /// <summary>
    /// GET /api/bookings/sales-reports/by-booking/{bookingId}
    /// Retrieves the authoritative sales report associated with a booking.
    /// </summary>
    [HttpGet("by-booking/{bookingId:guid}")]
    public async Task<ActionResult<SalesReportDto>> GetByBookingId(Guid bookingId, CancellationToken ct)
    {
        var report = await _salesReportingService.GetSalesReportByBookingIdAsync(bookingId, ct);
        if (report == null)
            return NotFound(new { message = $"Sales report for booking {bookingId} not found" });

        return Ok(SalesReportDto.FromEntity(report));
    }

    /// <summary>
    /// GET /api/bookings/sales-reports/by-event/{eventId}
    /// Retrieves all sales reports for an event.
    /// </summary>
    [HttpGet("by-event/{eventId:guid}")]
    public async Task<ActionResult<IReadOnlyList<SalesReportDto>>> GetByEventId(Guid eventId, CancellationToken ct)
    {
        var reports = await _salesReportingService.GetSalesReportsByEventIdAsync(eventId, ct);
        return Ok(reports.Select(SalesReportDto.FromEntity).ToList());
    }

    /// <summary>
    /// GET /api/bookings/sales-reports/by-organizer/{organizerId}
    /// Retrieves all sales reports for an organizer's events.
    /// </summary>
    [HttpGet("by-organizer/{organizerId:guid}")]
    public async Task<ActionResult<IReadOnlyList<SalesReportDto>>> GetByOrganizerId(Guid organizerId, CancellationToken ct)
    {
        var reports = await _salesReportingService.GetSalesReportsByOrganizerIdAsync(organizerId, ct);
        return Ok(reports.Select(SalesReportDto.FromEntity).ToList());
    }
}
