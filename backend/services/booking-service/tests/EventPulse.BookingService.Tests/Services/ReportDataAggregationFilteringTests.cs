using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

/// <summary>
/// EP-394: End-to-end unit tests verifying multi-dimensional report filtering in ReportDataAggregationService.
/// Covers:
/// 1. Line-item slicing for TicketTypeId in Bookings and Revenue reports.
/// 2. Category filtering via cross-service event scoping.
/// 3. BookingStatus filtering on Revenue reports (Confirmed vs Cancelled).
/// 4. Combined multi-filter evaluation using strict boolean AND logic.
/// 5. Contradictory filters yielding zero-valued reports without errors.
/// </summary>
public class ReportDataAggregationFilteringTests
{
    private static BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    [Fact]
    public async Task BookingsReport_WithTicketTypeFilter_SlicesTicketsAndTotalsToTargetTier()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var filteringService = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);
        var aggregationService = new ReportDataAggregationService(dbContext, clientMock.Object, filteringService, NullLogger<ReportDataAggregationService>.Instance);

        var eventId = Guid.NewGuid();
        var vipTierId = Guid.NewGuid();
        var gaTierId = Guid.NewGuid();

        // Booking 1: Confirmed with 2 VIP ($100 each) and 3 GA ($20 each)
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<BookingItem>
            {
                new() { TicketTypeId = vipTierId, TicketName = "VIP Pass", Quantity = 2, UnitPrice = 100m, Subtotal = 200m },
                new() { TicketTypeId = gaTierId, TicketName = "General Admission", Quantity = 3, UnitPrice = 20m, Subtotal = 60m }
            }
        });

        // Booking 2: Confirmed with 5 GA only
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<BookingItem>
            {
                new() { TicketTypeId = gaTierId, TicketName = "General Admission", Quantity = 5, UnitPrice = 20m, Subtotal = 100m }
            }
        });

        // Booking 3: Cancelled with 1 VIP
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Status = BookingStatus.Cancelled,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-2),
            CancelledAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<BookingItem>
            {
                new() { TicketTypeId = vipTierId, TicketName = "VIP Pass", Quantity = 1, UnitPrice = 100m, Subtotal = 100m }
            }
        });

        await dbContext.SaveChangesAsync();

        // Query filtering by VIP tier only
        var query = new BookingsReportQueryParameters
        {
            EventId = eventId,
            TicketTypeId = vipTierId
        };

        var result = await aggregationService.GetBookingsReportDataAsync(query);

        // Booking 1 and Booking 3 match VIP. Booking 2 does not.
        Assert.Equal(2, result.TotalBookings);
        Assert.Equal(1, result.ConfirmedBookings);
        Assert.Equal(1, result.CancelledBookings);

        // Active tickets must strictly count 2 VIP (not 2 + 3 = 5 from Booking 1)
        Assert.Equal(2, result.ActiveTicketsBooked);
        Assert.Equal(1, result.CancelledTickets);
        Assert.Equal(3, result.GrossTicketsSold);

        // Ticket breakdown must only reflect VIP
        Assert.Single(result.TicketTypesBreakdown);
        Assert.Equal(vipTierId, result.TicketTypesBreakdown[0].TicketTypeId);
        Assert.Equal(2, result.TicketTypesBreakdown[0].ActiveQuantity);
        Assert.Equal(1, result.TicketTypesBreakdown[0].CancelledQuantity);
        Assert.Equal(3, result.TicketTypesBreakdown[0].TotalQuantity);
    }

    [Fact]
    public async Task RevenueReport_WithTicketTypeFilter_SlicesRevenueAndRefundsToTargetTier()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var filteringService = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);
        var aggregationService = new ReportDataAggregationService(dbContext, clientMock.Object, filteringService, NullLogger<ReportDataAggregationService>.Instance);

        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var vipTierId = Guid.NewGuid();
        var gaTierId = Guid.NewGuid();

        // Sale 1: $260 total (VIP: 2 @ $100 = $200; GA: 3 @ $20 = $60 with 1 GA refunded @ $20)
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            EventId = eventId,
            OrganizerId = organizerId,
            Currency = "USD",
            SaleAmount = 260m,
            RefundedAmount = 20m,
            RefundStatus = SalesRefundStatus.PartiallyRefunded,
            PaymentTimestampUtc = DateTimeOffset.UtcNow.AddDays(-2),
            Items = new List<SalesReportItem>
            {
                new()
                {
                    TicketTypeId = vipTierId,
                    TicketTypeName = "VIP Pass",
                    Quantity = 2,
                    RefundedQuantity = 0,
                    UnitPrice = 100m,
                    Subtotal = 200m,
                    RefundedAmount = 0m
                },
                new()
                {
                    TicketTypeId = gaTierId,
                    TicketTypeName = "General Admission",
                    Quantity = 3,
                    RefundedQuantity = 1,
                    UnitPrice = 20m,
                    Subtotal = 60m,
                    RefundedAmount = 20m
                }
            }
        });

        // Sale 2: $100 total (GA only: 5 @ $20 = $100)
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            EventId = eventId,
            OrganizerId = organizerId,
            Currency = "USD",
            SaleAmount = 100m,
            RefundedAmount = 0m,
            PaymentTimestampUtc = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<SalesReportItem>
            {
                new()
                {
                    TicketTypeId = gaTierId,
                    TicketTypeName = "General Admission",
                    Quantity = 5,
                    UnitPrice = 20m,
                    Subtotal = 100m
                }
            }
        });

        await dbContext.SaveChangesAsync();

        // Query filtering by VIP tier only
        var vipQuery = new RevenueReportQueryParameters
        {
            EventId = eventId,
            TicketTypeId = vipTierId
        };

        var vipResult = await aggregationService.GetRevenueReportDataAsync(vipQuery);

        // Only Sale 1 matches VIP.
        Assert.Equal(1, vipResult.SuccessfulPurchaseCount);
        // Sliced financial metrics for VIP: Gross $200, Refunded $0, Net $200
        Assert.Equal(200m, vipResult.GrossRevenue);
        Assert.Equal(0m, vipResult.RefundedAmount);
        Assert.Equal(200m, vipResult.NetRevenue);

        // Currency breakdown must reflect sliced metrics
        Assert.Single(vipResult.CurrencyBreakdown);
        Assert.Equal(200m, vipResult.CurrencyBreakdown[0].GrossRevenue);
        Assert.Equal(2, vipResult.CurrencyBreakdown[0].TicketsSold);
        Assert.Equal(2, vipResult.CurrencyBreakdown[0].NetTicketsSold);

        // Query filtering by GA tier only
        var gaQuery = new RevenueReportQueryParameters
        {
            EventId = eventId,
            TicketTypeId = gaTierId
        };

        var gaResult = await aggregationService.GetRevenueReportDataAsync(gaQuery);

        // Both Sale 1 and Sale 2 match GA.
        Assert.Equal(2, gaResult.SuccessfulPurchaseCount);
        // Sliced GA gross: $60 (from Sale 1) + $100 (from Sale 2) = $160
        Assert.Equal(160m, gaResult.GrossRevenue);
        // Sliced GA refund: $20 (from Sale 1)
        Assert.Equal(20m, gaResult.RefundedAmount);
        Assert.Equal(140m, gaResult.NetRevenue);
        Assert.Equal(8, gaResult.CurrencyBreakdown[0].TicketsSold); // 3 + 5 = 8
        Assert.Equal(7, gaResult.CurrencyBreakdown[0].NetTicketsSold); // 8 - 1 = 7
    }

    [Fact]
    public async Task BookingsReport_WithCategoryFilter_ResolvesEventsCrossServiceAndFilters()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var filteringService = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);
        var aggregationService = new ReportDataAggregationService(dbContext, clientMock.Object, filteringService, NullLogger<ReportDataAggregationService>.Instance);

        var musicEventId = Guid.NewGuid();
        var sportsEventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventsByCategoryAsync("Music", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = musicEventId, Category = "Music", Title = "Rock Fest" }
            });

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            EventId = musicEventId,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Quantity = 3, Subtotal = 150m } }
        });

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            EventId = sportsEventId,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Quantity = 10, Subtotal = 500m } }
        });

        await dbContext.SaveChangesAsync();

        var query = new BookingsReportQueryParameters
        {
            Category = "Musical Concert" // Normalizes to "Music"
        };

        var result = await aggregationService.GetBookingsReportDataAsync(query);

        Assert.Equal(1, result.TotalBookings);
        Assert.Equal(3, result.ActiveTicketsBooked);
        Assert.Single(result.EventsBreakdown);
        Assert.Equal(musicEventId, result.EventsBreakdown[0].EventId);
    }

    [Fact]
    public async Task RevenueReport_WithContradictoryFilters_ReturnsZeroValuedMetrics()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var filteringService = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);
        var aggregationService = new ReportDataAggregationService(dbContext, clientMock.Object, filteringService, NullLogger<ReportDataAggregationService>.Instance);

        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        // Event belongs to organizerId
        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = eventId, OrganizerId = organizerId }
            });

        // But category "Sports" contains NO events
        clientMock.Setup(c => c.GetEventsByCategoryAsync("Sports", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>());

        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            OrganizerId = organizerId,
            SaleAmount = 500m,
            Currency = "USD",
            PaymentTimestampUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Query combining organizer and contradictory category
        var query = new RevenueReportQueryParameters
        {
            OrganizerId = organizerId,
            Category = "Sports"
        };

        var result = await aggregationService.GetRevenueReportDataAsync(query);

        Assert.NotNull(result);
        Assert.Equal(0m, result.GrossRevenue);
        Assert.Equal(0m, result.NetRevenue);
        Assert.Equal(0, result.SuccessfulPurchaseCount);
        Assert.Empty(result.CurrencyBreakdown);
        Assert.Empty(result.EventsBreakdown);
    }

    [Fact]
    public async Task RevenueReport_WithBookingStatusFilter_FiltersByRefundState()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var filteringService = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);
        var aggregationService = new ReportDataAggregationService(dbContext, clientMock.Object, filteringService, NullLogger<ReportDataAggregationService>.Instance);

        var eventId = Guid.NewGuid();

        // Active sale (Confirmed)
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            SaleAmount = 200m,
            RefundedAmount = 0m,
            RefundStatus = SalesRefundStatus.None,
            PaymentTimestampUtc = DateTimeOffset.UtcNow
        });

        // Cancelled / fully refunded sale
        dbContext.SalesReports.Add(new SalesReport
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            SaleAmount = 150m,
            RefundedAmount = 150m,
            RefundStatus = SalesRefundStatus.Refunded,
            PaymentTimestampUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        // Query with BookingStatus = Confirmed (excludes full refunds)
        var confirmedQuery = new RevenueReportQueryParameters
        {
            EventId = eventId,
            BookingStatus = "Confirmed"
        };
        var confirmedResult = await aggregationService.GetRevenueReportDataAsync(confirmedQuery);

        Assert.Equal(1, confirmedResult.SuccessfulPurchaseCount);
        Assert.Equal(200m, confirmedResult.GrossRevenue);

        // Query with BookingStatus = Cancelled (only full refunds)
        var cancelledQuery = new RevenueReportQueryParameters
        {
            EventId = eventId,
            BookingStatus = "Cancelled"
        };
        var cancelledResult = await aggregationService.GetRevenueReportDataAsync(cancelledQuery);

        Assert.Equal(1, cancelledResult.SuccessfulPurchaseCount);
        Assert.Equal(150m, cancelledResult.GrossRevenue);
        Assert.Equal(150m, cancelledResult.RefundedAmount);
    }
}
