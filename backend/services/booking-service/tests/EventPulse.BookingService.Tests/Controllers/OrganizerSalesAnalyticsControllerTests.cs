using System.Reflection;
using System.Security.Claims;
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

public class OrganizerSalesAnalyticsControllerTests
{
    private readonly Mock<IOrganizerSalesAnalyticsService> _serviceMock;
    private readonly OrganizerSalesAnalyticsController _controller;

    public OrganizerSalesAnalyticsControllerTests()
    {
        _serviceMock = new Mock<IOrganizerSalesAnalyticsService>();
        _controller = new OrganizerSalesAnalyticsController(
            _serviceMock.Object,
            NullLogger<OrganizerSalesAnalyticsController>.Instance);
    }

    private void SetUserContext(Guid? organizerId = null, string role = "Organizer")
    {
        var claims = new List<Claim>();
        if (organizerId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, organizerId.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public void Scenario10_And_11_Controller_HasAuthorizeOrganizerOnlyPolicy()
    {
        // Act: Inspect class-level Authorize attribute
        var authorizeAttribute = typeof(OrganizerSalesAnalyticsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        // Assert: Authorization policy is strictly OrganizerOnly
        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AppPolicies.OrganizerOnly, authorizeAttribute.Policy);
    }

    [Fact]
    public async Task Scenario1_GetOrganizerSalesSummary_ValidOrganizer_ReturnsOkWithSummary()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        SetUserContext(organizerId);

        var expectedSummary = new OrganizerSalesSummaryDto
        {
            GrossRevenue = 500m,
            RefundedAmount = 50m,
            NetRevenue = 450m,
            TicketsSold = 10,
            NetTicketsSold = 9,
            SuccessfulPurchaseCount = 4,
            Currency = "USD"
        };

        _serviceMock.Setup(s => s.GetOrganizerSalesSummaryAsync(organizerId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var result = await _controller.GetOrganizerSalesSummary(null, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OrganizerSalesSummaryDto>(okResult.Value);
        Assert.Equal(500m, dto.GrossRevenue);
        Assert.Equal(450m, dto.NetRevenue);
    }

    [Fact]
    public async Task Scenario2_GetEventSalesAnalytics_ValidOrganizerAndEvent_ReturnsOkWithAnalytics()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        SetUserContext(organizerId);

        var expectedAnalytics = new EventSalesAnalyticsDto
        {
            EventId = eventId,
            EventName = "Concert",
            GrossRevenue = 200m,
            NetRevenue = 200m,
            TicketsSold = 4,
            NetTicketsSold = 4,
            SuccessfulPurchaseCount = 2,
            Currency = "USD"
        };

        _serviceMock.Setup(s => s.GetEventSalesAnalyticsAsync(organizerId, eventId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAnalytics);

        // Act
        var result = await _controller.GetEventSalesAnalytics(eventId, null, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EventSalesAnalyticsDto>(okResult.Value);
        Assert.Equal(eventId, dto.EventId);
        Assert.Equal(200m, dto.GrossRevenue);
    }

    [Fact]
    public async Task Scenario12_And_13_GetEventSalesAnalytics_WhenNotAccessibleOrNotFound_Returns404NotFound()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        SetUserContext(organizerId);

        // Service returns null when not owned by organizer or when non-existent
        _serviceMock.Setup(s => s.GetEventSalesAnalyticsAsync(organizerId, eventId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSalesAnalyticsDto?)null);

        // Act
        var result = await _controller.GetEventSalesAnalytics(eventId, null, null, CancellationToken.None);

        // Assert: 404 Not Found returned (non-disclosing)
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetOrganizerSalesSummary_WhenFromAfterTo_ReturnsBadRequest()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        SetUserContext(organizerId);

        var from = DateTimeOffset.UtcNow.AddDays(1);
        var to = DateTimeOffset.UtcNow;

        // Act
        var result = await _controller.GetOrganizerSalesSummary(from, to, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetEventSalesAnalytics_WhenFromAfterTo_ReturnsBadRequest()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        SetUserContext(organizerId);

        var from = DateTimeOffset.UtcNow.AddDays(5);
        var to = DateTimeOffset.UtcNow;

        // Act
        var result = await _controller.GetEventSalesAnalytics(Guid.NewGuid(), from, to, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetOrganizerEventsSalesList_ValidPagination_ReturnsOk()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        SetUserContext(organizerId);

        var pagedResult = new PagedResult<EventSalesSummaryItemDto>
        {
            Items = new List<EventSalesSummaryItemDto>
            {
                new() { EventId = Guid.NewGuid(), EventName = "Tech Summit", GrossRevenue = 1000m, Currency = "USD" }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _serviceMock.Setup(s => s.GetOrganizerEventsSalesListAsync(organizerId, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetOrganizerEventsSalesList(null, null, null, 1, 20, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PagedResult<EventSalesSummaryItemDto>>(okResult.Value);
        Assert.Single(dto.Items);
        Assert.Equal("Tech Summit", dto.Items[0].EventName);
    }

    [Fact]
    public async Task GetOrganizerEventsSalesList_InvalidPageOrPageSize_ReturnsBadRequest()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        SetUserContext(organizerId);

        // Act 1: page < 1
        var result1 = await _controller.GetOrganizerEventsSalesList(null, null, null, page: 0, pageSize: 20, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result1.Result);

        // Act 2: pageSize > 100
        var result2 = await _controller.GetOrganizerEventsSalesList(null, null, null, page: 1, pageSize: 101, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result2.Result);
    }

    [Fact]
    public async Task GetOrganizerSalesSummary_WhenMissingIdentityClaim_ReturnsUnauthorized()
    {
        // Arrange
        SetUserContext(organizerId: null); // unauthenticated / missing claim

        // Act
        var result = await _controller.GetOrganizerSalesSummary(null, null, CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }
}
