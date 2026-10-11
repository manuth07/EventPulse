using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

public class OrganizerSalesAnalyticsServiceTests
{
    private BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    [Fact]
    public async Task Scenario1_And_3_GetOrganizerSalesSummary_AggregatesAcrossMultipleEventsCorrectly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId1 = Guid.NewGuid();
        var eventId2 = Guid.NewGuid();

        var sale1 = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = eventId1,
            OrganizerId = organizerId,
            SaleAmount = 100m,
            RefundedAmount = 20m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow.AddDays(-2),
            Items = new List<SalesReportItem>
            {
                new() { TicketTypeId = Guid.NewGuid(), TicketTypeName = "VIP", UnitPrice = 50m, Quantity = 2, RefundedQuantity = 1, Subtotal = 100m, RefundedAmount = 20m }
            }
        };

        var sale2 = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = eventId2,
            OrganizerId = organizerId,
            SaleAmount = 50m,
            RefundedAmount = 0m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<SalesReportItem>
            {
                new() { TicketTypeId = Guid.NewGuid(), TicketTypeName = "Standard", UnitPrice = 50m, Quantity = 1, RefundedQuantity = 0, Subtotal = 50m, RefundedAmount = 0m }
            }
        };

        dbContext.SalesReports.AddRange(sale1, sale2);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetOrganizerSalesSummaryAsync(organizerId);

        // Assert
        Assert.Equal(150m, result.GrossRevenue);
        Assert.Equal(20m, result.RefundedAmount);
        Assert.Equal(130m, result.NetRevenue);
        Assert.Equal(3, result.TicketsSold);
        Assert.Equal(2, result.NetTicketsSold);
        Assert.Equal(2, result.SuccessfulPurchaseCount);
        Assert.Equal("USD", result.Currency);
        Assert.Single(result.Currencies);
    }

    [Fact]
    public async Task Scenario2_And_4_GetEventSalesAnalytics_ReturnsCorrectMetricsAndTicketTypeBreakdown()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketType1Id = Guid.NewGuid();
        var ticketType2Id = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Music Festival",
                OrganizerId = organizerId,
                Status = "Published"
            });

        var sale = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = eventId,
            OrganizerId = organizerId,
            SaleAmount = 200m,
            RefundedAmount = 50m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow,
            Items = new List<SalesReportItem>
            {
                new() { TicketTypeId = ticketType1Id, TicketTypeName = "VIP", UnitPrice = 100m, Quantity = 1, RefundedQuantity = 0, Subtotal = 100m, RefundedAmount = 0m },
                new() { TicketTypeId = ticketType2Id, TicketTypeName = "General", UnitPrice = 50m, Quantity = 2, RefundedQuantity = 1, Subtotal = 100m, RefundedAmount = 50m }
            }
        };

        dbContext.SalesReports.Add(sale);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetEventSalesAnalyticsAsync(organizerId, eventId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal("Music Festival", result.EventName);
        Assert.Equal(200m, result.GrossRevenue);
        Assert.Equal(50m, result.RefundedAmount);
        Assert.Equal(150m, result.NetRevenue);
        Assert.Equal(3, result.TicketsSold);
        Assert.Equal(2, result.NetTicketsSold);
        Assert.Equal(1, result.SuccessfulPurchaseCount);
        Assert.Equal(2, result.TicketTypes.Count);

        var vip = result.TicketTypes.First(t => t.TicketTypeId == ticketType1Id);
        Assert.Equal(100m, vip.GrossRevenue);
        Assert.Equal(0m, vip.RefundedAmount);
        Assert.Equal(100m, vip.NetRevenue);
        Assert.Equal(1, vip.QuantitySold);

        var gen = result.TicketTypes.First(t => t.TicketTypeId == ticketType2Id);
        Assert.Equal(100m, gen.GrossRevenue);
        Assert.Equal(50m, gen.RefundedAmount);
        Assert.Equal(50m, gen.NetRevenue);
        Assert.Equal(2, gen.QuantitySold);
        Assert.Equal(1, gen.NetQuantitySold);
    }

    [Fact]
    public async Task Scenario5_NoSalesEvent_ReturnsZeroValuesWithoutError()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Brand New Event",
                OrganizerId = organizerId,
                Status = "Draft"
            });

        // Act
        var result = await service.GetEventSalesAnalyticsAsync(organizerId, eventId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0m, result.GrossRevenue);
        Assert.Equal(0m, result.RefundedAmount);
        Assert.Equal(0m, result.NetRevenue);
        Assert.Equal(0, result.TicketsSold);
        Assert.Equal(0, result.NetTicketsSold);
        Assert.Equal(0, result.SuccessfulPurchaseCount);
        Assert.Empty(result.TicketTypes);
    }

    [Fact]
    public async Task Scenario5_NoSalesOrganizer_ReturnsZeroValuesWithoutError()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();

        // Act
        var result = await service.GetOrganizerSalesSummaryAsync(organizerId);

        // Assert
        Assert.Equal(0m, result.GrossRevenue);
        Assert.Equal(0m, result.RefundedAmount);
        Assert.Equal(0m, result.NetRevenue);
        Assert.Equal(0, result.TicketsSold);
        Assert.Equal(0, result.NetTicketsSold);
        Assert.Equal(0, result.SuccessfulPurchaseCount);
    }

    [Fact]
    public async Task Scenario7_RefundAdjustmentAppliedCorrectly_NetRevenueReducesByRefund()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var sale = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = eventId,
            OrganizerId = organizerId,
            SaleAmount = 500m,
            RefundedAmount = 200m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow,
            Items = new List<SalesReportItem>
            {
                new() { TicketTypeId = Guid.NewGuid(), TicketTypeName = "General", UnitPrice = 100m, Quantity = 5, RefundedQuantity = 2, Subtotal = 500m, RefundedAmount = 200m }
            }
        };

        dbContext.SalesReports.Add(sale);
        await dbContext.SaveChangesAsync();

        // Act
        var summary = await service.GetOrganizerSalesSummaryAsync(organizerId);

        // Assert
        Assert.Equal(500m, summary.GrossRevenue);
        Assert.Equal(200m, summary.RefundedAmount);
        Assert.Equal(300m, summary.NetRevenue);
        Assert.Equal(5, summary.TicketsSold);
        Assert.Equal(3, summary.NetTicketsSold);
    }

    [Fact]
    public async Task Scenario9_And_12_AnotherOrganizersEvent_ReturnsNullFor404()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var authenticOrganizerId = Guid.NewGuid();
        var foreignOrganizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        // EventService reports foreign organizer ownership
        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Other's Event",
                OrganizerId = foreignOrganizerId
            });

        // Act
        var result = await service.GetEventSalesAnalyticsAsync(authenticOrganizerId, eventId);

        // Assert: Must return null (which translates to non-disclosing 404 in controller)
        Assert.Null(result);
    }

    [Fact]
    public async Task Scenario13_InvalidEventId_WhenEventDoesNotExist_ReturnsNullFor404()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var nonExistentEventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(nonExistentEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSummaryInfo?)null);

        // Act
        var result = await service.GetEventSalesAnalyticsAsync(organizerId, nonExistentEventId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Scenario14_DateFiltering_FiltersSalesByDateRangeCorrectly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var baseDate = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        var saleOld = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OrganizerId = organizerId,
            SaleAmount = 100m,
            Currency = "USD",
            PaymentTimestampUtc = baseDate.AddDays(-10),
            Items = new List<SalesReportItem>
            {
                new() { Quantity = 1, Subtotal = 100m }
            }
        };

        var saleInside = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OrganizerId = organizerId,
            SaleAmount = 250m,
            Currency = "USD",
            PaymentTimestampUtc = baseDate,
            Items = new List<SalesReportItem>
            {
                new() { Quantity = 2, Subtotal = 250m }
            }
        };

        var saleFuture = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OrganizerId = organizerId,
            SaleAmount = 300m,
            Currency = "USD",
            PaymentTimestampUtc = baseDate.AddDays(10),
            Items = new List<SalesReportItem>
            {
                new() { Quantity = 3, Subtotal = 300m }
            }
        };

        dbContext.SalesReports.AddRange(saleOld, saleInside, saleFuture);
        await dbContext.SaveChangesAsync();

        var from = baseDate.AddDays(-1);
        var to = baseDate.AddDays(1);

        // Act
        var result = await service.GetOrganizerSalesSummaryAsync(organizerId, from, to);

        // Assert
        Assert.Equal(250m, result.GrossRevenue);
        Assert.Equal(250m, result.NetRevenue);
        Assert.Equal(2, result.TicketsSold);
        Assert.Equal(1, result.SuccessfulPurchaseCount);
    }

    [Fact]
    public async Task Scenario15_CurrencyHandling_MultiCurrency_GroupsByCurrencySeparately()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();

        var saleUsd = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OrganizerId = organizerId,
            SaleAmount = 100m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow,
            Items = new List<SalesReportItem>
            {
                new() { Quantity = 1, Subtotal = 100m }
            }
        };

        var saleEur = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OrganizerId = organizerId,
            SaleAmount = 80m,
            RefundedAmount = 10m,
            Currency = "EUR",
            PaymentTimestampUtc = DateTimeOffset.UtcNow,
            Items = new List<SalesReportItem>
            {
                new() { Quantity = 2, Subtotal = 80m, RefundedAmount = 10m }
            }
        };

        dbContext.SalesReports.AddRange(saleUsd, saleEur);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetOrganizerSalesSummaryAsync(organizerId);

        // Assert: Multi-currency does not merge different currencies into one total
        Assert.Equal(2, result.Currencies.Count);
        var usdGroup = result.Currencies.First(c => c.Currency == "USD");
        Assert.Equal(100m, usdGroup.GrossRevenue);
        Assert.Equal(100m, usdGroup.NetRevenue);

        var eurGroup = result.Currencies.First(c => c.Currency == "EUR");
        Assert.Equal(80m, eurGroup.GrossRevenue);
        Assert.Equal(10m, eurGroup.RefundedAmount);
        Assert.Equal(70m, eurGroup.NetRevenue);

        Assert.Equal(3, result.TicketsSold);
        Assert.Equal(2, result.SuccessfulPurchaseCount);
    }

    [Fact]
    public async Task Scenario8_DuplicateSaleQuery_IdempotentAndConsistent()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var saleId = Guid.NewGuid();

        var sale = new SalesReport
        {
            Id = saleId,
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OrganizerId = organizerId,
            SaleAmount = 120m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow,
            Items = new List<SalesReportItem>
            {
                new() { Quantity = 2, Subtotal = 120m }
            }
        };

        dbContext.SalesReports.Add(sale);
        await dbContext.SaveChangesAsync();

        // Act - Call twice
        var run1 = await service.GetOrganizerSalesSummaryAsync(organizerId);
        var run2 = await service.GetOrganizerSalesSummaryAsync(organizerId);

        // Assert: Perfectly idempotent and consistent
        Assert.Equal(run1.GrossRevenue, run2.GrossRevenue);
        Assert.Equal(run1.TicketsSold, run2.TicketsSold);
        Assert.Equal(run1.SuccessfulPurchaseCount, run2.SuccessfulPurchaseCount);
    }

    [Fact]
    public async Task GetOrganizerEventsSalesListAsync_PaginationAndSearch_WorksCorrectly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerSalesAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerSalesAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var event1Id = Guid.NewGuid();
        var event2Id = Guid.NewGuid();

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = event1Id, Title = "Rock Concert", OrganizerId = organizerId },
                new() { Id = event2Id, Title = "Tech Conference", OrganizerId = organizerId }
            });

        var sale1 = new SalesReport
        {
            Id = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            EventId = event1Id,
            OrganizerId = organizerId,
            SaleAmount = 500m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow,
            Items = new List<SalesReportItem>
            {
                new() { Quantity = 5, Subtotal = 500m }
            }
        };

        dbContext.SalesReports.Add(sale1);
        await dbContext.SaveChangesAsync();

        // Act 1: Search "Rock"
        var searchResult = await service.GetOrganizerEventsSalesListAsync(organizerId, search: "Rock");

        // Assert 1
        Assert.Single(searchResult.Items);
        Assert.Equal("Rock Concert", searchResult.Items[0].EventName);
        Assert.Equal(500m, searchResult.Items[0].GrossRevenue);

        // Act 2: All items with pagination (page=1, pageSize=1)
        var pagedResult = await service.GetOrganizerEventsSalesListAsync(organizerId, page: 1, pageSize: 1);

        // Assert 2
        Assert.Equal(2, pagedResult.TotalCount);
        Assert.Single(pagedResult.Items);
        Assert.True(pagedResult.HasNextPage);
    }
}
