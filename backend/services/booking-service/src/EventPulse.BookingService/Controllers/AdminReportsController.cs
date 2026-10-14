using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventPulse.BookingService.Controllers;

/// <summary>
/// Administrative controller for generating dynamic platform reports (US-39 / EP-378).
/// Strictly protected by the EP-376 AdminOnly authorization policy.
/// Accessible via /api/admin/reports and /api/bookings/reports routes.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Route("api/bookings/reports")]
[Authorize(Policy = AppPolicies.AdminOnly)]
public class AdminReportsController : ControllerBase
{
    private static readonly HashSet<string> ValidBookingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "All", "Confirmed", "Cancelled", "PendingPayment", "PaymentFailed"
    };

    private static readonly HashSet<string> ValidPlatformSummaryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "platform-summary", "platform_summary", "platform", "platformactivitysummary"
    };

    private static readonly HashSet<string> ValidBookingsAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "bookings", "bookings-report", "bookings_report"
    };

    private static readonly HashSet<string> ValidRevenueAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "revenue", "revenue-report", "revenue_report"
    };

    private readonly IReportDataAggregationService _aggregationService;
    private readonly IEventAvailabilityClient? _eventAvailabilityClient;
    private readonly ILogger<AdminReportsController> _logger;

    public AdminReportsController(
        IReportDataAggregationService aggregationService,
        ILogger<AdminReportsController> logger)
        : this(aggregationService, null, logger)
    {
    }

    public AdminReportsController(
        IReportDataAggregationService aggregationService,
        IEventAvailabilityClient? eventAvailabilityClient,
        ILogger<AdminReportsController> logger)
    {
        _aggregationService = aggregationService;
        _eventAvailabilityClient = eventAvailabilityClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/reports/bookings
    /// Generates dynamic Bookings Report covering confirmed/cancelled orders, ticket churn, and event breakdowns.
    /// </summary>
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookingsReport(
        [FromQuery] BookingsReportQueryParameters query,
        CancellationToken ct)
    {
        var dateValidation = ValidateDateRange(query.From, query.To);
        if (!dateValidation.IsValid)
            return dateValidation.ErrorResult!;

        var statusValidation = ValidateBookingStatus(query.BookingStatus);
        if (!statusValidation.IsValid)
            return statusValidation.ErrorResult!;

        var categoryValidation = ValidateCategory(query.Category);
        if (!categoryValidation.IsValid)
            return categoryValidation.ErrorResult!;

        var ticketValidation = await ValidateTicketTypeDependencyAsync(query.EventId, query.TicketTypeId, ct);
        if (!ticketValidation.IsValid)
            return ticketValidation.ErrorResult!;

        var result = await _aggregationService.GetBookingsReportDataAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/reports/revenue
    /// Generates dynamic Revenue Report covering gross/net revenue, refunds, and multi-currency segregation.
    /// </summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenueReport(
        [FromQuery] RevenueReportQueryParameters query,
        CancellationToken ct)
    {
        var dateValidation = ValidateDateRange(query.From, query.To);
        if (!dateValidation.IsValid)
            return dateValidation.ErrorResult!;

        var statusValidation = ValidateBookingStatus(query.BookingStatus);
        if (!statusValidation.IsValid)
            return statusValidation.ErrorResult!;

        var currencyValidation = ValidateCurrency(query.Currency);
        if (!currencyValidation.IsValid)
            return currencyValidation.ErrorResult!;

        var categoryValidation = ValidateCategory(query.Category);
        if (!categoryValidation.IsValid)
            return categoryValidation.ErrorResult!;

        var ticketValidation = await ValidateTicketTypeDependencyAsync(query.EventId, query.TicketTypeId, ct);
        if (!ticketValidation.IsValid)
            return ticketValidation.ErrorResult!;

        var result = await _aggregationService.GetRevenueReportDataAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/reports/platform-summary
    /// Generates high-level executive Platform Activity Summary report.
    /// </summary>
    [HttpGet("platform-summary")]
    public async Task<IActionResult> GetPlatformSummaryReport(
        [FromQuery] PlatformActivityReportQueryParameters query,
        CancellationToken ct)
    {
        var dateValidation = ValidateDateRange(query.From, query.To);
        if (!dateValidation.IsValid)
            return dateValidation.ErrorResult!;

        var statusValidation = ValidateBookingStatus(query.BookingStatus);
        if (!statusValidation.IsValid)
            return statusValidation.ErrorResult!;

        var currencyValidation = ValidateCurrency(query.Currency);
        if (!currencyValidation.IsValid)
            return currencyValidation.ErrorResult!;

        var categoryValidation = ValidateCategory(query.Category);
        if (!categoryValidation.IsValid)
            return categoryValidation.ErrorResult!;

        var ticketValidation = await ValidateTicketTypeDependencyAsync(query.EventId, query.TicketTypeId, ct);
        if (!ticketValidation.IsValid)
            return ticketValidation.ErrorResult!;

        var result = await _aggregationService.GetPlatformActivityReportDataAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/reports
    /// Unified query endpoint accepting reportType as query parameter.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReportByQuery(
        [FromQuery] string? reportType,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? eventId,
        [FromQuery] Guid? organizerId,
        [FromQuery] string? bookingStatus,
        [FromQuery] string? currency,
        [FromQuery] Guid? ticketTypeId,
        [FromQuery] string? category,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reportType))
        {
            return BadRequest(new
            {
                code = "MISSING_REPORT_TYPE",
                message = "The 'reportType' parameter is required. Supported types are: platform-summary, bookings, revenue."
            });
        }

        return await GenerateReportInternal(reportType, from, to, eventId, organizerId, bookingStatus, currency, ticketTypeId, category, ct);
    }

    [NonAction]
    public Task<IActionResult> GetReportByQuery(
        string? reportType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? eventId,
        Guid? organizerId,
        string? bookingStatus,
        string? currency,
        CancellationToken ct) => GetReportByQuery(reportType, from, to, eventId, organizerId, bookingStatus, currency, null, null, ct);

    /// <summary>
    /// GET /api/admin/reports/{reportType}
    /// Route-based report generator endpoint.
    /// </summary>
    [HttpGet("{reportType}")]
    public async Task<IActionResult> GetReportByType(
        [FromRoute] string reportType,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? eventId,
        [FromQuery] Guid? organizerId,
        [FromQuery] string? bookingStatus,
        [FromQuery] string? currency,
        [FromQuery] Guid? ticketTypeId,
        [FromQuery] string? category,
        CancellationToken ct)
    {
        return await GenerateReportInternal(reportType, from, to, eventId, organizerId, bookingStatus, currency, ticketTypeId, category, ct);
    }

    [NonAction]
    public Task<IActionResult> GetReportByType(
        string reportType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? eventId,
        Guid? organizerId,
        string? bookingStatus,
        string? currency,
        CancellationToken ct) => GetReportByType(reportType, from, to, eventId, organizerId, bookingStatus, currency, null, null, ct);

    /// <summary>
    /// POST /api/admin/reports/generate
    /// Dynamic report generation endpoint accepting structured filter criteria in request body.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateReport(
        [FromBody] GenerateReportRequest request,
        CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ReportType))
        {
            return BadRequest(new
            {
                code = "MISSING_REPORT_TYPE",
                message = "The 'reportType' field is required. Supported types are: platform-summary, bookings, revenue."
            });
        }

        var normalizedType = request.ReportType.Trim();
        var dateValidation = ValidateDateRange(request.From, request.To);
        if (!dateValidation.IsValid)
            return dateValidation.ErrorResult!;

        var statusValidation = ValidateBookingStatus(request.BookingStatus);
        if (!statusValidation.IsValid)
            return statusValidation.ErrorResult!;

        var categoryValidation = ValidateCategory(request.Category);
        if (!categoryValidation.IsValid)
            return categoryValidation.ErrorResult!;

        var ticketValidation = await ValidateTicketTypeDependencyAsync(request.EventId, request.TicketTypeId, ct);
        if (!ticketValidation.IsValid)
            return ticketValidation.ErrorResult!;

        if (ValidBookingsAliases.Contains(normalizedType))
        {
            var data = await _aggregationService.GetBookingsReportDataAsync(new BookingsReportQueryParameters
            {
                From = request.From,
                To = request.To,
                EventId = request.EventId,
                OrganizerId = request.OrganizerId,
                BookingStatus = request.BookingStatus,
                TicketTypeId = request.TicketTypeId,
                Category = request.Category
            }, ct);

            return Ok(new GeneratedReportEnvelopeDto
            {
                ReportType = "bookings",
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                Data = data
            });
        }

        if (ValidRevenueAliases.Contains(normalizedType))
        {
            var data = await _aggregationService.GetRevenueReportDataAsync(new RevenueReportQueryParameters
            {
                From = request.From,
                To = request.To,
                EventId = request.EventId,
                OrganizerId = request.OrganizerId,
                Currency = request.Currency,
                TicketTypeId = request.TicketTypeId,
                Category = request.Category,
                BookingStatus = request.BookingStatus
            }, ct);

            return Ok(new GeneratedReportEnvelopeDto
            {
                ReportType = "revenue",
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                Data = data
            });
        }

        if (ValidPlatformSummaryAliases.Contains(normalizedType))
        {
            var data = await _aggregationService.GetPlatformActivityReportDataAsync(new PlatformActivityReportQueryParameters
            {
                From = request.From,
                To = request.To,
                EventId = request.EventId,
                OrganizerId = request.OrganizerId,
                BookingStatus = request.BookingStatus,
                Currency = request.Currency,
                TicketTypeId = request.TicketTypeId,
                Category = request.Category
            }, ct);

            return Ok(new GeneratedReportEnvelopeDto
            {
                ReportType = "platform-summary",
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                Data = data
            });
        }

        return BadRequest(new
        {
            code = "UNSUPPORTED_REPORT_TYPE",
            message = $"Unsupported report type '{request.ReportType}'. Supported types are: platform-summary, bookings, revenue."
        });
    }

    private async Task<IActionResult> GenerateReportInternal(
        string reportType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? eventId,
        Guid? organizerId,
        string? bookingStatus,
        string? currency,
        Guid? ticketTypeId,
        string? category,
        CancellationToken ct)
    {
        var trimmedType = reportType.Trim();
        var dateValidation = ValidateDateRange(from, to);
        if (!dateValidation.IsValid)
            return dateValidation.ErrorResult!;

        var statusValidation = ValidateBookingStatus(bookingStatus);
        if (!statusValidation.IsValid)
            return statusValidation.ErrorResult!;

        var currencyValidation = ValidateCurrency(currency);
        if (!currencyValidation.IsValid)
            return currencyValidation.ErrorResult!;

        var categoryValidation = ValidateCategory(category);
        if (!categoryValidation.IsValid)
            return categoryValidation.ErrorResult!;

        var ticketValidation = await ValidateTicketTypeDependencyAsync(eventId, ticketTypeId, ct);
        if (!ticketValidation.IsValid)
            return ticketValidation.ErrorResult!;

        if (ValidBookingsAliases.Contains(trimmedType))
        {
            var data = await _aggregationService.GetBookingsReportDataAsync(new BookingsReportQueryParameters
            {
                From = from,
                To = to,
                EventId = eventId,
                OrganizerId = organizerId,
                BookingStatus = bookingStatus,
                TicketTypeId = ticketTypeId,
                Category = category
            }, ct);

            return Ok(data);
        }

        if (ValidRevenueAliases.Contains(trimmedType))
        {
            var data = await _aggregationService.GetRevenueReportDataAsync(new RevenueReportQueryParameters
            {
                From = from,
                To = to,
                EventId = eventId,
                OrganizerId = organizerId,
                Currency = currency,
                TicketTypeId = ticketTypeId,
                Category = category,
                BookingStatus = bookingStatus
            }, ct);

            return Ok(data);
        }

        if (ValidPlatformSummaryAliases.Contains(trimmedType))
        {
            var data = await _aggregationService.GetPlatformActivityReportDataAsync(new PlatformActivityReportQueryParameters
            {
                From = from,
                To = to,
                EventId = eventId,
                OrganizerId = organizerId,
                BookingStatus = bookingStatus,
                Currency = currency,
                TicketTypeId = ticketTypeId,
                Category = category
            }, ct);

            return Ok(data);
        }

        return BadRequest(new
        {
            code = "UNSUPPORTED_REPORT_TYPE",
            message = $"Unsupported report type '{reportType}'. Supported types are: platform-summary, bookings, revenue."
        });
    }

    private (bool IsValid, IActionResult? ErrorResult) ValidateDateRange(DateTimeOffset? from, DateTimeOffset? to)
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

        return (true, null);
    }

    private (bool IsValid, IActionResult? ErrorResult) ValidateBookingStatus(string? bookingStatus)
    {
        if (!string.IsNullOrWhiteSpace(bookingStatus) && !ValidBookingStatuses.Contains(bookingStatus.Trim()))
        {
            return (false, BadRequest(new
            {
                code = "INVALID_BOOKING_STATUS",
                message = $"Invalid booking status '{bookingStatus}'. Supported values are: All, Confirmed, Cancelled, PendingPayment, PaymentFailed."
            }));
        }

        return (true, null);
    }

    private (bool IsValid, IActionResult? ErrorResult) ValidateCurrency(string? currency)
    {
        if (!string.IsNullOrWhiteSpace(currency) && !currency.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = currency.Trim();
            if (trimmed.Length != 3 || !trimmed.All(char.IsLetter))
            {
                return (false, BadRequest(new
                {
                    code = "INVALID_CURRENCY",
                    message = $"Currency '{currency}' must be a valid 3-letter ISO-4217 code (e.g. 'USD', 'LKR')."
                }));
            }
        }

        return (true, null);
    }

    private (bool IsValid, IActionResult? ErrorResult) ValidateCategory(string? category)
    {
        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (!ReportCategories.IsValid(category))
            {
                return (false, BadRequest(new
                {
                    code = "INVALID_CATEGORY",
                    message = $"Unsupported event category '{category}'. Valid categories are Music, Sports, Conference, Workshop, Festival, Arts & Theatre, Community, Other."
                }));
            }
        }

        return (true, null);
    }

    private async Task<(bool IsValid, IActionResult? ErrorResult)> ValidateTicketTypeDependencyAsync(
        Guid? eventId,
        Guid? ticketTypeId,
        CancellationToken ct)
    {
        if (ticketTypeId.HasValue && !eventId.HasValue)
        {
            return (false, BadRequest(new
            {
                code = "MISSING_EVENT_FOR_TIER",
                message = "An event must be selected when filtering by ticket type."
            }));
        }

        if (ticketTypeId.HasValue && eventId.HasValue && _eventAvailabilityClient != null)
        {
            try
            {
                var ticketType = await _eventAvailabilityClient.GetTicketTypeAsync(eventId.Value, ticketTypeId.Value, ct);
                if (ticketType == null)
                {
                    return (false, BadRequest(new
                    {
                        code = "INVALID_TICKET_TYPE",
                        message = "The specified ticket type does not belong to the selected event."
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to verify ticket type {TicketTypeId} for event {EventId}", ticketTypeId.Value, eventId.Value);
            }
        }

        return (true, null);
    }
}
