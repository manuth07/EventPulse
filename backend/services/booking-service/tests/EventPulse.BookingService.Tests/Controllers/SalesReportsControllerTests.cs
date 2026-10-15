using EventPulse.BookingService.Controllers;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Controllers;

public class SalesReportsControllerTests
{
    private readonly Mock<ISalesReportingService> _serviceMock;
    private readonly SalesReportsController _controller;

    public SalesReportsControllerTests()
    {
        _serviceMock = new Mock<ISalesReportingService>();
        _controller = new SalesReportsController(_serviceMock.Object, NullLogger<SalesReportsController>.Instance);
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithDto()
    {
        var saleId = Guid.NewGuid();
        var report = new SalesReport
        {
            Id = saleId,
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            SaleAmount = 100m,
            Currency = "USD"
        };
        _serviceMock.Setup(s => s.GetSalesReportByIdAsync(saleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var result = await _controller.GetById(saleId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SalesReportDto>(okResult.Value);
        Assert.Equal(saleId, dto.Id);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        _serviceMock.Setup(s => s.GetSalesReportByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesReport?)null);

        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByBookingId_WhenFound_ReturnsOk()
    {
        var bookingId = Guid.NewGuid();
        var report = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            SaleAmount = 50m
        };
        _serviceMock.Setup(s => s.GetSalesReportByBookingIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var result = await _controller.GetByBookingId(bookingId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<SalesReportDto>(okResult.Value);
        Assert.Equal(bookingId, dto.BookingId);
    }

    [Fact]
    public async Task GetByEventId_ReturnsListOfDtos()
    {
        var eventId = Guid.NewGuid();
        var reports = new List<SalesReport>
        {
            new() { Id = Guid.NewGuid(), EventId = eventId, SaleAmount = 50m },
            new() { Id = Guid.NewGuid(), EventId = eventId, SaleAmount = 75m }
        };
        _serviceMock.Setup(s => s.GetSalesReportsByEventIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        var result = await _controller.GetByEventId(eventId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<SalesReportDto>>(okResult.Value);
        Assert.Equal(2, dtos.Count);
    }

    [Fact]
    public async Task GetByOrganizerId_ReturnsListOfDtos()
    {
        var organizerId = Guid.NewGuid();
        var reports = new List<SalesReport>
        {
            new() { Id = Guid.NewGuid(), OrganizerId = organizerId, SaleAmount = 120m }
        };
        _serviceMock.Setup(s => s.GetSalesReportsByOrganizerIdAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        var result = await _controller.GetByOrganizerId(organizerId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<SalesReportDto>>(okResult.Value);
        Assert.Single(dtos);
    }

    // =========================================================================
    // EP-405: GET /api/bookings/sales-reports Collection Endpoint Tests
    // =========================================================================

    [Fact]
    public async Task GetAll_WhenReportsExist_ReturnsOkWithPagedResult()
    {
        // Arrange
        var filter = new ReportFilterDto { Page = 1, PageSize = 10 };
        var expectedReports = new List<SalesReportDto>
        {
            new() { Id = Guid.NewGuid(), BookingReference = "BK-001", SaleAmount = 150m, Currency = "USD" },
            new() { Id = Guid.NewGuid(), BookingReference = "BK-002", SaleAmount = 200m, Currency = "USD" }
        };

        var pagedResult = new PagedResult<SalesReportDto>
        {
            Items = expectedReports,
            Page = 1,
            PageSize = 10,
            TotalCount = 2
        };

        _serviceMock.Setup(s => s.GetSalesReportsAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(filter, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedPaged = Assert.IsType<PagedResult<SalesReportDto>>(okResult.Value);
        Assert.Equal(2, returnedPaged.TotalCount);
        Assert.Equal(2, returnedPaged.Items.Count);
        Assert.Equal("BK-001", returnedPaged.Items[0].BookingReference);
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsOkWithEmptyPagedResult()
    {
        // Arrange
        var filter = new ReportFilterDto { Page = 1, PageSize = 20 };
        var pagedResult = new PagedResult<SalesReportDto>
        {
            Items = Array.Empty<SalesReportDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };

        _serviceMock.Setup(s => s.GetSalesReportsAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAll(filter, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedPaged = Assert.IsType<PagedResult<SalesReportDto>>(okResult.Value);
        Assert.Equal(0, returnedPaged.TotalCount);
        Assert.Empty(returnedPaged.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAll_WhenPageLessThanOne_ReturnsBadRequest(int invalidPage)
    {
        // Arrange
        var filter = new ReportFilterDto { Page = invalidPage, PageSize = 20 };

        // Act
        var result = await _controller.GetAll(filter, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        _serviceMock.Verify(s => s.GetSalesReportsAsync(It.IsAny<ReportFilterDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public async Task GetAll_WhenPageSizeOutOfRange_ReturnsBadRequest(int invalidPageSize)
    {
        // Arrange
        var filter = new ReportFilterDto { Page = 1, PageSize = invalidPageSize };

        // Act
        var result = await _controller.GetAll(filter, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        _serviceMock.Verify(s => s.GetSalesReportsAsync(It.IsAny<ReportFilterDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_WhenFromAfterTo_ReturnsBadRequest()
    {
        // Arrange
        var filter = new ReportFilterDto
        {
            From = DateTimeOffset.UtcNow.AddDays(5),
            To = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _controller.GetAll(filter, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        _serviceMock.Verify(s => s.GetSalesReportsAsync(It.IsAny<ReportFilterDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_WhenDateRangeExceeds730Days_ReturnsBadRequest()
    {
        // Arrange
        var filter = new ReportFilterDto
        {
            From = DateTimeOffset.UtcNow.AddDays(-800),
            To = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _controller.GetAll(filter, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        _serviceMock.Verify(s => s.GetSalesReportsAsync(It.IsAny<ReportFilterDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_WhenCategoryInvalid_ReturnsBadRequest()
    {
        // Arrange
        var filter = new ReportFilterDto
        {
            Category = "CompletelyNonExistentCategory"
        };

        // Act
        var result = await _controller.GetAll(filter, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        _serviceMock.Verify(s => s.GetSalesReportsAsync(It.IsAny<ReportFilterDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_WhenTicketTypeIdWithoutEventId_ReturnsBadRequest()
    {
        // Arrange
        var filter = new ReportFilterDto
        {
            TicketTypeId = Guid.NewGuid(),
            EventId = null // Missing eventId
        };

        // Act
        var result = await _controller.GetAll(filter, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        _serviceMock.Verify(s => s.GetSalesReportsAsync(It.IsAny<ReportFilterDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_PropagatesCancellationToken()
    {
        // Arrange
        var filter = new ReportFilterDto { Page = 1, PageSize = 10 };
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _serviceMock.Setup(s => s.GetSalesReportsAsync(filter, ct))
            .ReturnsAsync(new PagedResult<SalesReportDto>());

        // Act
        await _controller.GetAll(filter, ct);

        // Assert
        _serviceMock.Verify(s => s.GetSalesReportsAsync(filter, ct), Times.Once);
    }
}
