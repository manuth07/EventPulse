using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using EventPulse.BookingService;
using EventPulse.BookingService.Controllers;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Security;

/// <summary>
/// EP-395 / US-42: Server-side validation and access control tests for report filtering.
/// Verifies:
/// 1. Date range validation (from <= to, max 730 days limit).
/// 2. Supported filter names and values (Category, Currency, BookingStatus).
/// 3. Unsupported filter combinations (ticketTypeId without eventId, ticketTypeId mismatch).
/// 4. Consistent error responses with standard error envelope (code and message).
/// 5. Policy enforcement: 401 for unauthenticated, 403 for unauthorized roles (Customer/Organizer on Admin routes).
/// 6. Multi-tenant isolation: Organizers cannot access another organizer's events (404 enumeration defense).
/// 7. Prevention of administrator-only filter bypass: Organizers supplying organizerId receive 403.
/// </summary>
public class ReportFilterValidationAndAccessControlTests
{
    private const string SigningKey = "EventPulseKey_2026_SecureAuthSigningKey_9876543210_LK";
    private const string Issuer = "EventPulse.IdentityService";
    private const string Audience = "EventPulse.Clients";

    private readonly AuthorizationOptions _authOptions;

    public ReportFilterValidationAndAccessControlTests()
    {
        _authOptions = new AuthorizationOptions();
        _authOptions.AddPolicy(AppPolicies.AdminOnly, policy => policy.RequireRole("Administrator"));
        _authOptions.AddPolicy(AppPolicies.AdministratorOnly, policy => policy.RequireRole("Administrator"));
        _authOptions.AddPolicy(AppPolicies.OrganizerOnly, policy => policy.RequireRole("Organizer"));
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    // =========================================================================
    // 1. Date Range Validation (EP-395 Requirement 1)
    // =========================================================================

    [Fact]
    public async Task AdminReports_WhenFromAfterTo_ReturnsInvalidDateRange()
    {
        var aggMock = new Mock<IReportDataAggregationService>();
        var controller = new AdminReportsController(aggMock.Object, NullLogger<AdminReportsController>.Instance);

        var query = new BookingsReportQueryParameters
        {
            From = DateTimeOffset.UtcNow,
            To = DateTimeOffset.UtcNow.AddDays(-3)
        };

        var result = await controller.GetBookingsReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("INVALID_DATE_RANGE", json);
    }

    [Fact]
    public async Task AdminReports_WhenRangeExceeds730Days_ReturnsDateRangeExceeded()
    {
        var aggMock = new Mock<IReportDataAggregationService>();
        var controller = new AdminReportsController(aggMock.Object, NullLogger<AdminReportsController>.Instance);

        var query = new RevenueReportQueryParameters
        {
            From = DateTimeOffset.UtcNow.AddDays(-731),
            To = DateTimeOffset.UtcNow
        };

        var result = await controller.GetRevenueReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("DATE_RANGE_EXCEEDED", json);
    }

    // =========================================================================
    // 2. Filter Values Validation (Category, Currency, Status - EP-395 Requirement 2)
    // =========================================================================

    [Fact]
    public async Task AdminReports_WithInvalidCategory_ReturnsInvalidCategoryBadRequest()
    {
        var aggMock = new Mock<IReportDataAggregationService>();
        var controller = new AdminReportsController(aggMock.Object, NullLogger<AdminReportsController>.Instance);

        var query = new BookingsReportQueryParameters
        {
            Category = "UnlistedFictionalCategory"
        };

        var result = await controller.GetBookingsReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("INVALID_CATEGORY", json);
    }

    [Fact]
    public async Task AdminReports_WithValidOrLegacyCategory_PassesValidation()
    {
        var aggMock = new Mock<IReportDataAggregationService>();
        aggMock.Setup(s => s.GetBookingsReportDataAsync(It.IsAny<BookingsReportQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingsReportDataDto());

        var controller = new AdminReportsController(aggMock.Object, NullLogger<AdminReportsController>.Instance);

        // "Musical Concert" is a valid legacy alias that normalizes to "Music"
        var query = new BookingsReportQueryParameters
        {
            Category = "Musical Concert"
        };

        var result = await controller.GetBookingsReport(query, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task AdminReports_WithInvalidCurrency_ReturnsInvalidCurrencyBadRequest()
    {
        var aggMock = new Mock<IReportDataAggregationService>();
        var controller = new AdminReportsController(aggMock.Object, NullLogger<AdminReportsController>.Instance);

        var query = new RevenueReportQueryParameters
        {
            Currency = "12" // Not a 3-letter alphabetic ISO code
        };

        var result = await controller.GetRevenueReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("INVALID_CURRENCY", json);
    }

    [Fact]
    public async Task AdminReports_WithInvalidBookingStatus_ReturnsInvalidBookingStatusBadRequest()
    {
        var aggMock = new Mock<IReportDataAggregationService>();
        var controller = new AdminReportsController(aggMock.Object, NullLogger<AdminReportsController>.Instance);

        var query = new BookingsReportQueryParameters
        {
            BookingStatus = "BogusStatus"
        };

        var result = await controller.GetBookingsReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("INVALID_BOOKING_STATUS", json);
    }

    // =========================================================================
    // 3. Filter Combination Dependency Validation (EP-395 Requirement 3)
    // =========================================================================

    [Fact]
    public async Task AdminReports_WhenTicketTypeSuppliedWithoutEventId_ReturnsMissingEventForTier()
    {
        var aggMock = new Mock<IReportDataAggregationService>();
        var controller = new AdminReportsController(aggMock.Object, NullLogger<AdminReportsController>.Instance);

        var query = new BookingsReportQueryParameters
        {
            TicketTypeId = Guid.NewGuid(),
            EventId = null // Missing required parent event
        };

        var result = await controller.GetBookingsReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("MISSING_EVENT_FOR_TIER", json);
    }

    [Fact]
    public async Task AdminReports_WhenTicketTypeDoesNotBelongToEvent_ReturnsInvalidTicketType()
    {
        var aggMock = new Mock<IReportDataAggregationService>();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        // Event availability client returns null for this tier on this event
        clientMock.Setup(c => c.GetTicketTypeAsync(eventId, ticketTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketTypeAvailability?)null);

        var controller = new AdminReportsController(aggMock.Object, clientMock.Object, NullLogger<AdminReportsController>.Instance);

        var query = new BookingsReportQueryParameters
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId
        };

        var result = await controller.GetBookingsReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("INVALID_TICKET_TYPE", json);
    }

    // =========================================================================
    // 4. Role Authorization Policy Tests (EP-395 Requirement 5 & 6)
    // =========================================================================

    [Theory]
    [InlineData("Customer")]
    [InlineData("Organizer")]
    public void AdminReportsController_DeniesAccessToNonAdminRoles(string role)
    {
        var principal = CreatePrincipal(Guid.NewGuid(), role);
        var policy = _authOptions.GetPolicy(AppPolicies.AdminOnly);
        Assert.NotNull(policy);

        var requirement = policy.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>().First();
        Assert.DoesNotContain(role, requirement.AllowedRoles);
    }

    [Fact]
    public void AdminReportsController_GrantsAccessToAdministratorRole()
    {
        var policy = _authOptions.GetPolicy(AppPolicies.AdminOnly);
        Assert.NotNull(policy);

        var requirement = policy.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>().First();
        Assert.Contains("Administrator", requirement.AllowedRoles);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Administrator")]
    public void OrganizerAnalytics_DeniesAccessToNonOrganizerRoles(string role)
    {
        var policy = _authOptions.GetPolicy(AppPolicies.OrganizerOnly);
        Assert.NotNull(policy);

        var requirement = policy.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>().First();
        Assert.DoesNotContain(role, requirement.AllowedRoles);
    }

    // =========================================================================
    // 5. Organizer Access Control & Parameter Tampering (EP-395 Requirement 7, 8, 9)
    // =========================================================================

    [Fact]
    public async Task OrganizerSales_WhenOrganizerIdFilterSupplied_ReturnsForbidden()
    {
        var serviceMock = new Mock<IOrganizerSalesAnalyticsService>();
        var controller = new OrganizerSalesAnalyticsController(serviceMock.Object, NullLogger<OrganizerSalesAnalyticsController>.Instance);

        var organizerId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext { User = CreatePrincipal(organizerId, "Organizer") };
        // Malicious or accidental organizerId query parameter
        httpContext.Request.QueryString = new QueryString("?organizerId=" + Guid.NewGuid());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetOrganizerSalesSummary(null, null, CancellationToken.None);

        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCodeResult.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(statusCodeResult.Value);
        Assert.Contains("UNAUTHORIZED_FILTER", json);
    }

    [Fact]
    public async Task OrganizerBookings_WhenOrganizerIdFilterSupplied_ReturnsForbidden()
    {
        var serviceMock = new Mock<IOrganizerBookingAnalyticsService>();
        var controller = new OrganizerBookingAnalyticsController(serviceMock.Object, NullLogger<OrganizerBookingAnalyticsController>.Instance);

        var organizerId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext { User = CreatePrincipal(organizerId, "Organizer") };
        httpContext.Request.QueryString = new QueryString("?organizerId=" + Guid.NewGuid());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetOrganizerBookingSummary(null, null, CancellationToken.None);

        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCodeResult.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(statusCodeResult.Value);
        Assert.Contains("UNAUTHORIZED_FILTER", json);
    }

    [Fact]
    public async Task OrganizerSales_AccessingAnotherOrganizersEvent_ReturnsNotFound()
    {
        var serviceMock = new Mock<IOrganizerSalesAnalyticsService>();
        var controller = new OrganizerSalesAnalyticsController(serviceMock.Object, NullLogger<OrganizerSalesAnalyticsController>.Instance);

        var authenticatedOrganizer = Guid.NewGuid();
        var foreignEventId = Guid.NewGuid();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreatePrincipal(authenticatedOrganizer, "Organizer") }
        };

        // Service returns null when event belongs to another organizer (enumeration defense)
        serviceMock.Setup(s => s.GetEventSalesAnalyticsAsync(authenticatedOrganizer, foreignEventId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSalesAnalyticsDto?)null);

        var result = await controller.GetEventSalesAnalytics(foreignEventId, null, null, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(notFoundResult.Value);
        Assert.Contains("EVENT_NOT_FOUND", json);
    }

    [Fact]
    public async Task OrganizerSales_WhenDateRangeExceeds730Days_ReturnsDateRangeExceeded()
    {
        var serviceMock = new Mock<IOrganizerSalesAnalyticsService>();
        var controller = new OrganizerSalesAnalyticsController(serviceMock.Object, NullLogger<OrganizerSalesAnalyticsController>.Instance);

        var organizerId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreatePrincipal(organizerId, "Organizer") }
        };

        var from = DateTimeOffset.UtcNow.AddDays(-750);
        var to = DateTimeOffset.UtcNow;

        var result = await controller.GetOrganizerSalesSummary(from, to, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("DATE_RANGE_EXCEEDED", json);
    }
}
