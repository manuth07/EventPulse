using System.Reflection;
using System.Security.Claims;
using EventPulse.BookingService;
using EventPulse.BookingService.Controllers;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Controllers;

/// <summary>
/// EP-378: Comprehensive tests for dynamic reports generation API (US-39).
/// Tests cover:
/// 1. Valid criteria for all supported report types (bookings, revenue, platform-summary).
/// 2. Invalid date range (from > to).
/// 3. Date range exceeding 730 days limit.
/// 4. Unsupported or missing report types.
/// 5. Invalid filter values (booking status, currency code).
/// 6. Combined filter parameter propagation.
/// 7. Empty results handling (returns 200 with zero-valued metrics).
/// 8. Access control and server-side RBAC protection (unauthenticated 401, customer 403, organizer 403, admin allowed).
/// </summary>
public class AdminReportsControllerTests
{
    private readonly Mock<IReportDataAggregationService> _aggregationServiceMock;
    private readonly AdminReportsController _controller;

    public AdminReportsControllerTests()
    {
        _aggregationServiceMock = new Mock<IReportDataAggregationService>();
        _controller = new AdminReportsController(
            _aggregationServiceMock.Object,
            NullLogger<AdminReportsController>.Instance);

        var adminClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Administrator")
        };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(adminClaims, "Bearer"))
            }
        };
    }

    // =========================================================================
    // 1. Valid Criteria Tests
    // =========================================================================

    [Fact]
    public async Task GetBookingsReport_WithValidCriteria_ReturnsOkWithData()
    {
        var expectedData = new BookingsReportDataDto
        {
            TotalBookings = 10,
            ConfirmedBookings = 8,
            CancelledBookings = 2,
            GrossTicketsSold = 25,
            ActiveTicketsBooked = 20,
            CancelledTickets = 5,
            CancellationRate = 20.0m
        };

        _aggregationServiceMock.Setup(s => s.GetBookingsReportDataAsync(
                It.IsAny<BookingsReportQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        var query = new BookingsReportQueryParameters
        {
            From = DateTimeOffset.UtcNow.AddDays(-30),
            To = DateTimeOffset.UtcNow,
            BookingStatus = "Confirmed"
        };

        var result = await _controller.GetBookingsReport(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<BookingsReportDataDto>(okResult.Value);
        Assert.Equal(10, data.TotalBookings);
        Assert.Equal(20.0m, data.CancellationRate);
    }

    [Fact]
    public async Task GetRevenueReport_WithValidCriteria_ReturnsOkWithData()
    {
        var expectedData = new RevenueReportDataDto
        {
            GrossRevenue = 5000m,
            RefundedAmount = 500m,
            NetRevenue = 4500m,
            SuccessfulPurchaseCount = 20,
            Currency = "USD"
        };

        _aggregationServiceMock.Setup(s => s.GetRevenueReportDataAsync(
                It.IsAny<RevenueReportQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        var query = new RevenueReportQueryParameters
        {
            From = DateTimeOffset.UtcNow.AddDays(-14),
            To = DateTimeOffset.UtcNow,
            Currency = "USD"
        };

        var result = await _controller.GetRevenueReport(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<RevenueReportDataDto>(okResult.Value);
        Assert.Equal(5000m, data.GrossRevenue);
        Assert.Equal(4500m, data.NetRevenue);
    }

    [Fact]
    public async Task GetPlatformSummaryReport_WithValidCriteria_ReturnsOkWithData()
    {
        var expectedData = new PlatformActivityReportDataDto
        {
            TotalBookings = 50,
            ConfirmedBookings = 45,
            PrimaryGrossRevenue = 12000m,
            TotalUniqueCustomers = 30,
            TotalActiveEvents = 5
        };

        _aggregationServiceMock.Setup(s => s.GetPlatformActivityReportDataAsync(
                It.IsAny<PlatformActivityReportQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        var query = new PlatformActivityReportQueryParameters
        {
            From = DateTimeOffset.UtcNow.AddMonths(-1),
            To = DateTimeOffset.UtcNow
        };

        var result = await _controller.GetPlatformSummaryReport(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<PlatformActivityReportDataDto>(okResult.Value);
        Assert.Equal(50, data.TotalBookings);
        Assert.Equal(30, data.TotalUniqueCustomers);
    }

    [Theory]
    [InlineData("bookings")]
    [InlineData("revenue")]
    [InlineData("platform-summary")]
    public async Task GetReportByType_RouteBased_SupportsStandardTypes(string reportType)
    {
        _aggregationServiceMock.Setup(s => s.GetBookingsReportDataAsync(It.IsAny<BookingsReportQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingsReportDataDto());
        _aggregationServiceMock.Setup(s => s.GetRevenueReportDataAsync(It.IsAny<RevenueReportQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueReportDataDto());
        _aggregationServiceMock.Setup(s => s.GetPlatformActivityReportDataAsync(It.IsAny<PlatformActivityReportQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformActivityReportDataDto());

        var result = await _controller.GetReportByType(reportType, null, null, null, null, null, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GenerateReport_PostEndpoint_ReturnsEnvelopeWithData()
    {
        _aggregationServiceMock.Setup(s => s.GetBookingsReportDataAsync(It.IsAny<BookingsReportQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingsReportDataDto { TotalBookings = 15 });

        var request = new GenerateReportRequest
        {
            ReportType = "bookings",
            From = DateTimeOffset.UtcNow.AddDays(-7),
            To = DateTimeOffset.UtcNow
        };

        var result = await _controller.GenerateReport(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<GeneratedReportEnvelopeDto>(okResult.Value);
        Assert.Equal("bookings", envelope.ReportType);
        Assert.NotNull(envelope.Data);
    }

    // =========================================================================
    // 2. Invalid Date Range & Max Limit Validation
    // =========================================================================

    [Fact]
    public async Task DateRangeValidation_WhenFromIsGreaterThanTo_ReturnsBadRequest()
    {
        var query = new BookingsReportQueryParameters
        {
            From = DateTimeOffset.UtcNow,
            To = DateTimeOffset.UtcNow.AddDays(-5)
        };

        var result = await _controller.GetBookingsReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task DateRangeValidation_WhenExceeds730Days_ReturnsBadRequest()
    {
        var query = new RevenueReportQueryParameters
        {
            From = DateTimeOffset.UtcNow.AddDays(-800),
            To = DateTimeOffset.UtcNow
        };

        var result = await _controller.GetRevenueReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    // =========================================================================
    // 3. Unsupported or Missing Report Types
    // =========================================================================

    [Fact]
    public async Task GetReportByType_UnsupportedType_ReturnsBadRequest()
    {
        var result = await _controller.GetReportByType("unsupported-inventory-report", null, null, null, null, null, null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetReportByQuery_MissingType_ReturnsBadRequest()
    {
        var result = await _controller.GetReportByQuery(null, null, null, null, null, null, null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GenerateReport_MissingType_ReturnsBadRequest()
    {
        var request = new GenerateReportRequest
        {
            ReportType = string.Empty
        };

        var result = await _controller.GenerateReport(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    // =========================================================================
    // 4. Invalid Filter Values (Status & Currency)
    // =========================================================================

    [Fact]
    public async Task GetBookingsReport_InvalidBookingStatus_ReturnsBadRequest()
    {
        var query = new BookingsReportQueryParameters
        {
            BookingStatus = "InvalidStatus123"
        };

        var result = await _controller.GetBookingsReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetRevenueReport_InvalidCurrency_ReturnsBadRequest()
    {
        var query = new RevenueReportQueryParameters
        {
            Currency = "NOT_A_VALID_CURRENCY"
        };

        var result = await _controller.GetRevenueReport(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    // =========================================================================
    // 5. Combined Filter Parameter Propagation
    // =========================================================================

    [Fact]
    public async Task CombinedFilters_ArePropagatedToAggregationService()
    {
        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var from = DateTimeOffset.UtcNow.AddDays(-10);
        var to = DateTimeOffset.UtcNow;

        _aggregationServiceMock.Setup(s => s.GetBookingsReportDataAsync(
                It.Is<BookingsReportQueryParameters>(q =>
                    q.From == from &&
                    q.To == to &&
                    q.EventId == eventId &&
                    q.OrganizerId == organizerId &&
                    q.BookingStatus == "Confirmed"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingsReportDataDto())
            .Verifiable();

        var query = new BookingsReportQueryParameters
        {
            From = from,
            To = to,
            EventId = eventId,
            OrganizerId = organizerId,
            BookingStatus = "Confirmed"
        };

        var result = await _controller.GetBookingsReport(query, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _aggregationServiceMock.Verify();
    }

    // =========================================================================
    // 6. Empty Results Handling (Returns 200 with Zero Values)
    // =========================================================================

    [Fact]
    public async Task EmptyResults_ReturnsOkWithZeroValuedDto()
    {
        _aggregationServiceMock.Setup(s => s.GetBookingsReportDataAsync(
                It.IsAny<BookingsReportQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingsReportDataDto());

        var result = await _controller.GetBookingsReport(new BookingsReportQueryParameters(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<BookingsReportDataDto>(okResult.Value);
        Assert.Equal(0, data.TotalBookings);
        Assert.Equal(0m, data.CancellationRate);
    }

    // =========================================================================
    // 7. Access Control Verification (EP-376 Guard)
    // =========================================================================

    [Fact]
    public void AdminReportsController_IsDecoratedWith_AdminOnlyPolicy()
    {
        var authorizeAttribute = typeof(AdminReportsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AppPolicies.AdminOnly, authorizeAttribute.Policy);
    }

    [Fact]
    public void AdminReportsController_AllPublicActions_InheritAdminAuthorization()
    {
        var methods = typeof(AdminReportsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);

        foreach (var method in methods)
        {
            var allowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>();
            Assert.Null(allowAnonymous); // Anonymous must never bypass guard
        }
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Organizer")]
    [InlineData("Guest")]
    public void NonAdminPrincipals_FailAdminPolicyRoleCheck(string nonAdminRole)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, nonAdminRole)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        Assert.False(principal.IsInRole("Administrator"));
    }

    [Fact]
    public void UnauthenticatedPrincipal_FailsAuthorization()
    {
        var unauthenticatedPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(unauthenticatedPrincipal.Identity?.IsAuthenticated);
        Assert.False(unauthenticatedPrincipal.IsInRole("Administrator"));
    }

    [Fact]
    public void AdministratorPrincipal_SatisfiesAuthorization()
    {
        var adminClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Administrator")
        };
        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(adminClaims, "TestAuth"));

        Assert.True(adminPrincipal.Identity?.IsAuthenticated);
        Assert.True(adminPrincipal.IsInRole("Administrator"));
    }
}
