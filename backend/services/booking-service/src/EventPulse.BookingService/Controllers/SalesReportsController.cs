using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Services;

namespace EventPulse.BookingService.Controllers;

[ApiController]
[Route("api/bookings/sales-reports")]
[Authorize]
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
