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

public class OrganizerBookingAnalyticsControllerTests
{
    private readonly Mock<IOrganizerBookingAnalyticsService> _serviceMock;
    private readonly OrganizerBookingAnalyticsController _controller;

    public OrganizerBookingAnalyticsControllerTests()
    {
        _serviceMock = new Mock<IOrganizerBookingAnalyticsService>();
        _controller = new OrganizerBookingAnalyticsController(
            _serviceMock.Object,
            NullLogger<OrganizerBookingAnalyticsController>.Instance);
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
    public void Scenario1_Controller_HasAuthorizeOrganizerOnlyPolicy()
    {
        // Act: Inspect class-level Authorize attribute
        var authorizeAttribute = typeof(OrganizerBookingAnalyticsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        // Assert: Authorization policy is strictly OrganizerOnly
        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AppPolicies.OrganizerOnly, authorizeAttribute.Policy);
    }

    [Fact]
    public async Task Scenario2_GetOrganizerBookingSummary_ValidOrganizer_ReturnsOkWithSummary()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        SetUserContext(organizerId);

        var expectedSummary = new OrganizerBookingSummaryDto
        {
            TotalBookings = 10,
            ConfirmedBookings = 8,
            CancelledBookings = 2,
            TotalTicketsBooked = 25,
            ActiveTicketsBooked = 20,
            CancelledTickets = 5,
            CancellationRate = 20.00m,
            AverageTicketsPerBooking = 2.50m
        };

        _serviceMock.Setup(s => s.GetOrganizerBookingSummaryAsync(organizerId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSummary);

        // Act
        var result = await _controller.GetOrganizerBookingSummary(null, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OrganizerBookingSummaryDto>(okResult.Value);
        Assert.Equal(10, dto.TotalBookings);
        Assert.Equal(8, dto.ConfirmedBookings);
        Assert.Equal(20.00m, dto.CancellationRate);
    }

    [Fact]
    public async Task Scenario3_GetOrganizerBookingSummary_MissingIdentity_ReturnsUnauthorized()
    {
        // Arrange: No user claims set
        SetUserContext(organizerId: null);

        // Act
        var result = await _controller.GetOrganizerBookingSummary(null, null, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task Scenario4_GetOrganizerBookingSummary_InvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        SetUserContext(organizerId);

        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(-1); // from > to

        // Act
        var result = await _controller.GetOrganizerBookingSummary(from, to, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task Scenario5_GetEventBookingAnalytics_ValidEvent_ReturnsOk()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        SetUserContext(organizerId);

        var expectedAnalytics = new EventBookingAnalyticsDto
        {
            EventId = eventId,
            EventName = "Art Expo",
            TotalBookings = 5,
            ConfirmedBookings = 5,
            TotalTicketsBooked = 10,
            ActiveTicketsBooked = 10,
            TotalCapacity = 50,
            RemainingCapacity = 40,
            CapacityUtilizationRate = 20.00m
        };

        _serviceMock.Setup(s => s.GetEventBookingAnalyticsAsync(organizerId, eventId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAnalytics);

        // Act
        var result = await _controller.GetEventBookingAnalytics(eventId, null, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<EventBookingAnalyticsDto>(okResult.Value);
        Assert.Equal(eventId, dto.EventId);
        Assert.Equal(5, dto.TotalBookings);
        Assert.Equal(50, dto.TotalCapacity);
    }

    [Fact]
    public async Task Scenario6_GetEventBookingAnalytics_EventNotFoundOrUnauthorized_ReturnsNotFound()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        SetUserContext(organizerId);

        // Service returns null when not found or owned by someone else
        _serviceMock.Setup(s => s.GetEventBookingAnalyticsAsync(organizerId, eventId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventBookingAnalyticsDto?)null);

        // Act
        var result = await _controller.GetEventBookingAnalytics(eventId, null, null, CancellationToken.None);

        // Assert: Non-disclosing 404
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task Scenario7_GetOrganizerEventsBookingList_ValidRequest_ReturnsOkWithPagedResult()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        SetUserContext(organizerId);

        var expectedPaged = new PagedResult<EventBookingSummaryItemDto>
        {
            Items = new List<EventBookingSummaryItemDto>
            {
                new() { EventId = Guid.NewGuid(), EventName = "Summit 2026", TotalBookings = 15, ConfirmedBookings = 12 }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _serviceMock.Setup(s => s.GetOrganizerEventsBookingListAsync(organizerId, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPaged);

        // Act
        var result = await _controller.GetOrganizerEventsBookingList(null, null, null, 1, 20, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResult<EventBookingSummaryItemDto>>(okResult.Value);
        Assert.Single(paged.Items);
        Assert.Equal("Summit 2026", paged.Items[0].EventName);
    }

    [Fact]
    public async Task Scenario8_GetOrganizerEventsBookingList_InvalidPagination_ReturnsBadRequest()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        SetUserContext(organizerId);

        // Act: Page < 1
        var resultPageZero = await _controller.GetOrganizerEventsBookingList(null, null, null, page: 0, pageSize: 20);
        Assert.IsType<BadRequestObjectResult>(resultPageZero.Result);

        // Act: PageSize > 100
        var resultPageSizeTooLarge = await _controller.GetOrganizerEventsBookingList(null, null, null, page: 1, pageSize: 101);
        Assert.IsType<BadRequestObjectResult>(resultPageSizeTooLarge.Result);
    }
}
