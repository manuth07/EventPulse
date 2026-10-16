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
/// EP-394 / US-42: Unit tests for ReportFilteringService.
/// Verifies:
/// 1. Cross-service event scope resolution across organizer, category, and event constraints.
/// 2. Category normalization ("Musical Concert" -> "Music").
/// 3. Boolean AND logic combination and contradiction resolution (empty result).
/// 4. EF Queryable filtering for Bookings and SalesReports.
/// 5. Line-item slicing for TicketTypeId filtering across gross/net tickets and revenue.
/// </summary>
public class ReportFilteringServiceTests
{
    private static BookingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new BookingDbContext(options);
    }

    // =========================================================================
    // 1. ResolveAllowedEventIdsAsync Scope Tests
    // =========================================================================

    [Fact]
    public async Task ResolveAllowedEventIds_WhenNoConstraints_ReturnsNullUnrestricted()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);

        var result = await service.ResolveAllowedEventIdsAsync(null, null, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAllowedEventIds_WithEventIdOnly_ReturnsTargetEventId()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);
        var eventId = Guid.NewGuid();

        var result = await service.ResolveAllowedEventIdsAsync(null, null, eventId);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(eventId, result[0]);
    }

    [Fact]
    public async Task ResolveAllowedEventIds_WithEventIdAndMatchingOrganizer_ReturnsTargetEventId()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);
        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = eventId, OrganizerId = organizerId, Title = "Match Event" }
            });

        var result = await service.ResolveAllowedEventIdsAsync(organizerId, null, eventId);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(eventId, result[0]);
    }

    [Fact]
    public async Task ResolveAllowedEventIds_WithEventIdAndMismatchedOrganizer_ReturnsEmptyContradiction()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);
        var eventId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = Guid.NewGuid(), OrganizerId = organizerId, Title = "Other Event" }
            });

        var result = await service.ResolveAllowedEventIdsAsync(organizerId, null, eventId);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ResolveAllowedEventIds_WithCategoryOnly_NormalizesAndReturnsMatchingEvents()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);
        var event1 = Guid.NewGuid();
        var event2 = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventsByCategoryAsync("Music", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = event1, Category = "Music" },
                new() { Id = event2, Category = "Music" }
            });

        // Supplying legacy alias "Musical Concert" which normalizes to "Music"
        var result = await service.ResolveAllowedEventIdsAsync(null, "Musical Concert", null);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(event1, result);
        Assert.Contains(event2, result);
    }

    [Fact]
    public async Task ResolveAllowedEventIds_WithOrganizerAndCategory_IntersectsBothSets()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);

        var sharedEvent = Guid.NewGuid();
        var orgOnlyEvent = Guid.NewGuid();
        var catOnlyEvent = Guid.NewGuid();
        var organizerId = Guid.NewGuid();

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = sharedEvent, OrganizerId = organizerId },
                new() { Id = orgOnlyEvent, OrganizerId = organizerId }
            });

        clientMock.Setup(c => c.GetEventsByCategoryAsync("Sports", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = sharedEvent, Category = "Sports" },
                new() { Id = catOnlyEvent, Category = "Sports" }
            });

        var result = await service.ResolveAllowedEventIdsAsync(organizerId, "Sports", null);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(sharedEvent, result[0]);
    }

    [Fact]
    public async Task ResolveAllowedEventIds_WhenClientThrows_ReturnsEmptyListGracefully()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);

        clientMock.Setup(c => c.GetOrganizerEventsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Event service down"));

        var result = await service.ResolveAllowedEventIdsAsync(Guid.NewGuid(), null, null);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // =========================================================================
    // 2. Line-Item Slicing Tests
    // =========================================================================

    [Fact]
    public void LineItemSlicing_Booking_CalculatesCorrectTicketsForTier()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);

        var vipTierId = Guid.NewGuid();
        var standardTierId = Guid.NewGuid();

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            Items = new List<BookingItem>
            {
                new() { TicketTypeId = vipTierId, Quantity = 2, UnitPrice = 100m, Subtotal = 200m },
                new() { TicketTypeId = standardTierId, Quantity = 5, UnitPrice = 30m, Subtotal = 150m }
            }
        };

        // All items when no filter
        var allTickets = service.GetFilteredBookingTickets(booking, null);
        Assert.Equal(7, allTickets);

        // VIP items only
        var vipTickets = service.GetFilteredBookingTickets(booking, vipTierId);
        Assert.Equal(2, vipTickets);

        // Non-existent tier
        var missingTierTickets = service.GetFilteredBookingTickets(booking, Guid.NewGuid());
        Assert.Equal(0, missingTierTickets);
    }

    [Fact]
    public void LineItemSlicing_SalesReport_CalculatesCorrectRevenueAndChurnForTier()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);

        var vipTierId = Guid.NewGuid();
        var standardTierId = Guid.NewGuid();

        var report = new SalesReport
        {
            Id = Guid.NewGuid(),
            SaleAmount = 350m,
            RefundedAmount = 50m,
            Items = new List<SalesReportItem>
            {
                new()
                {
                    TicketTypeId = vipTierId,
                    Quantity = 2,
                    RefundedQuantity = 0,
                    UnitPrice = 100m,
                    Subtotal = 200m,
                    RefundedAmount = 0m
                },
                new()
                {
                    TicketTypeId = standardTierId,
                    Quantity = 5,
                    RefundedQuantity = 1,
                    UnitPrice = 30m,
                    Subtotal = 150m,
                    RefundedAmount = 30m
                }
            }
        };

        // Unfiltered
        Assert.Equal(350m, service.GetFilteredGrossRevenue(report, null));
        Assert.Equal(50m, service.GetFilteredRefundedAmount(report, null));
        Assert.Equal(300m, service.GetFilteredNetRevenue(report, null));
        Assert.Equal(7, service.GetFilteredTicketsSold(report, null));
        Assert.Equal(6, service.GetFilteredNetTicketsSold(report, null));

        // Sliced by VIP
        Assert.Equal(200m, service.GetFilteredGrossRevenue(report, vipTierId));
        Assert.Equal(0m, service.GetFilteredRefundedAmount(report, vipTierId));
        Assert.Equal(200m, service.GetFilteredNetRevenue(report, vipTierId));
        Assert.Equal(2, service.GetFilteredTicketsSold(report, vipTierId));
        Assert.Equal(2, service.GetFilteredNetTicketsSold(report, vipTierId));

        // Sliced by Standard
        Assert.Equal(150m, service.GetFilteredGrossRevenue(report, standardTierId));
        Assert.Equal(30m, service.GetFilteredRefundedAmount(report, standardTierId));
        Assert.Equal(120m, service.GetFilteredNetRevenue(report, standardTierId));
        Assert.Equal(5, service.GetFilteredTicketsSold(report, standardTierId));
        Assert.Equal(4, service.GetFilteredNetTicketsSold(report, standardTierId));
    }

    // =========================================================================
    // 3. Query Application Tests
    // =========================================================================

    [Fact]
    public async Task ApplyBookingFilters_CombinedFilters_ExecutesBooleanAnd()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new ReportFilteringService(dbContext, clientMock.Object, NullLogger<ReportFilteringService>.Instance);

        var event1 = Guid.NewGuid();
        var event2 = Guid.NewGuid();
        var vipTier = Guid.NewGuid();
        var standardTier = Guid.NewGuid();

        var b1 = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = event1,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero),
            Items = new List<BookingItem> { new() { TicketTypeId = vipTier, Quantity = 2 } }
        };

        var b2 = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = event1,
            Status = BookingStatus.Cancelled,
            ConfirmedAt = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero),
            Items = new List<BookingItem> { new() { TicketTypeId = vipTier, Quantity = 1 } }
        };

        var b3 = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = event2,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero),
            Items = new List<BookingItem> { new() { TicketTypeId = vipTier, Quantity = 3 } }
        };

        var b4 = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = event1,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero),
            Items = new List<BookingItem> { new() { TicketTypeId = standardTier, Quantity = 4 } }
        };

        dbContext.Bookings.AddRange(b1, b2, b3, b4);
        await dbContext.SaveChangesAsync();

        // AND criteria: Event1 + Confirmed + vipTier
        var criteria = new ReportFilterCriteria
        {
            EventId = event1,
            BookingStatus = "Confirmed",
            TicketTypeId = vipTier
        };

        var filteredQuery = service.ApplyBookingFilters(dbContext.Bookings.Include(b => b.Items), criteria, null);
        var results = await filteredQuery.ToListAsync();

        Assert.Single(results);
        Assert.Equal(b1.Id, results[0].Id);
    }
}
