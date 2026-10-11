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
}
