using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

public class OrganizerBookingAnalyticsServiceTests
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
    public async Task Scenario1_And_3_GetOrganizerBookingSummary_AggregatesAcrossMultipleEventsCorrectly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId1 = Guid.NewGuid();
        var eventId2 = Guid.NewGuid();

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = eventId1, Title = "Tech Summit", OrganizerId = organizerId },
                new() { Id = eventId2, Title = "Music Fest", OrganizerId = organizerId }
            });

        // Booking 1: Confirmed (2 tickets)
        var booking1 = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-101",
            EventId = eventId1,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Items = new List<BookingItem>
            {
                new() { Id = Guid.NewGuid(), TicketTypeId = Guid.NewGuid(), TicketName = "VIP", Quantity = 2, UnitPrice = 50m }
            }
        };

        // Booking 2: Cancelled (1 ticket)
        var booking2 = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-102",
            EventId = eventId2,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Cancelled,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-3),
            CancelledAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<BookingItem>
            {
                new() { Id = Guid.NewGuid(), TicketTypeId = Guid.NewGuid(), TicketName = "General", Quantity = 1, UnitPrice = 25m }
            }
        };

        dbContext.Bookings.AddRange(booking1, booking2);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetOrganizerBookingSummaryAsync(organizerId);

        // Assert
        Assert.Equal(2, result.TotalBookings); // 1 Confirmed + 1 Cancelled
        Assert.Equal(1, result.ConfirmedBookings);
        Assert.Equal(1, result.CancelledBookings);
        Assert.Equal(3, result.TotalTicketsBooked); // 2 + 1
        Assert.Equal(2, result.ActiveTicketsBooked);
        Assert.Equal(1, result.CancelledTickets);
        Assert.Equal(50.00m, result.CancellationRate); // (1 / 2) * 100
        Assert.Equal(33.33m, result.TicketCancellationRate); // (1 / 3) * 100
        Assert.Equal(2.00m, result.AverageTicketsPerBooking);
    }

    [Fact]
    public async Task Scenario4_5_6_BookingCounts_DistinguishConfirmedAndCancelledAccurately()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>
            {
                new() { Id = eventId, Title = "Gala Dinner", OrganizerId = organizerId }
            });

        // 3 Confirmed, 2 Cancelled
        for (int i = 0; i < 3; i++)
        {
            dbContext.Bookings.Add(new Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = $"BK-CONF-{i}",
                EventId = eventId,
                CustomerId = Guid.NewGuid(),
                Status = BookingStatus.Confirmed,
                ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 2, TicketName = "Standard" } }
            });
        }

        for (int i = 0; i < 2; i++)
        {
            dbContext.Bookings.Add(new Booking
            {
                Id = Guid.NewGuid(),
                BookingReference = $"BK-CANC-{i}",
                EventId = eventId,
                CustomerId = Guid.NewGuid(),
                Status = BookingStatus.Cancelled,
                ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-2),
                CancelledAt = DateTimeOffset.UtcNow.AddDays(-1),
                Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 1, TicketName = "Standard" } }
            });
        }

        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetOrganizerBookingSummaryAsync(organizerId);

        // Assert
        Assert.Equal(5, result.TotalBookings);
        Assert.Equal(3, result.ConfirmedBookings);
        Assert.Equal(2, result.CancelledBookings);
        Assert.Equal(8, result.TotalTicketsBooked); // (3 * 2) + (2 * 1) = 8
        Assert.Equal(6, result.ActiveTicketsBooked);
        Assert.Equal(2, result.CancelledTickets);
        Assert.Equal(40.00m, result.CancellationRate); // (2 / 5) * 100
        Assert.Equal(25.00m, result.TicketCancellationRate); // (2 / 8) * 100
    }

    [Fact]
    public async Task Scenario7_And_8_MultipleTicketTypes_AggregatesTierLineItemsCorrectly()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var vipTierId = Guid.NewGuid();
        var genTierId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, Title = "Festival", OrganizerId = organizerId });

        clientMock.Setup(c => c.GetEventTicketTypesAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TicketTypeAvailability>
            {
                new() { Id = vipTierId, Name = "VIP Pass", TotalQuantity = 50, RemainingQuantity = 48 },
                new() { Id = genTierId, Name = "General Admission", TotalQuantity = 200, RemainingQuantity = 197 }
            });

        // 1 Booking containing BOTH VIP (2) and General (3)
        var multiItemBooking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-MULTI",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items = new List<BookingItem>
            {
                new() { Id = Guid.NewGuid(), TicketTypeId = vipTierId, TicketName = "VIP Pass", Quantity = 2 },
                new() { Id = Guid.NewGuid(), TicketTypeId = genTierId, TicketName = "General Admission", Quantity = 3 }
            }
        };

        dbContext.Bookings.Add(multiItemBooking);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetEventBookingAnalyticsAsync(organizerId, eventId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalBookings);
        Assert.Equal(1, result.ConfirmedBookings);
        Assert.Equal(5, result.TotalTicketsBooked);
        Assert.Equal(5, result.ActiveTicketsBooked);
        Assert.Equal(250, result.TotalCapacity); // 50 + 200
        Assert.Equal(245, result.RemainingCapacity); // 48 + 197

        Assert.Equal(2, result.TicketTypes.Count);
        var vip = result.TicketTypes.First(t => t.TicketTypeId == vipTierId);
        var gen = result.TicketTypes.First(t => t.TicketTypeId == genTierId);

        Assert.Equal(1, vip.BookingsCount);
        Assert.Equal(2, vip.ActiveTickets);
        Assert.Equal(50, vip.Capacity);
        Assert.Equal(48, vip.RemainingCapacity);

        Assert.Equal(1, gen.BookingsCount);
        Assert.Equal(3, gen.ActiveTickets);
        Assert.Equal(200, gen.Capacity);
        Assert.Equal(197, gen.RemainingCapacity);
    }

    [Fact]
    public async Task Scenario10_ZeroBookingEvent_ReturnsZerosGracefullyWithoutError()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo { Id = eventId, Title = "Empty Event", OrganizerId = organizerId });

        clientMock.Setup(c => c.GetEventTicketTypesAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TicketTypeAvailability>
            {
                new() { Id = Guid.NewGuid(), Name = "Standard", TotalQuantity = 100, RemainingQuantity = 100 }
            });

        // Act
        var result = await service.GetEventBookingAnalyticsAsync(organizerId, eventId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalBookings);
        Assert.Equal(0, result.ConfirmedBookings);
        Assert.Equal(0, result.CancelledBookings);
        Assert.Equal(0, result.TotalTicketsBooked);
        Assert.Equal(0, result.ActiveTicketsBooked);
        Assert.Equal(0m, result.CancellationRate);
        Assert.Equal(100, result.TotalCapacity);
        Assert.Equal(100, result.RemainingCapacity);
        Assert.Equal(0m, result.CapacityUtilizationRate);
    }

    [Fact]
    public async Task Scenario11_DateRangeFiltering_FiltersStrictlyOnConfirmedAt()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo> { new() { Id = eventId, Title = "Dated Event", OrganizerId = organizerId } });

        var now = DateTimeOffset.UtcNow;

        // Inside range (Day -3)
        var bookingIn = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-IN",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = now.AddDays(-3),
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 2 } }
        };

        // Outside range (Day -10)
        var bookingOut = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-OUT",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = now.AddDays(-10),
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 3 } }
        };

        dbContext.Bookings.AddRange(bookingIn, bookingOut);
        await dbContext.SaveChangesAsync();

        // Act: Filter from Day -5 to Day -1
        var result = await service.GetOrganizerBookingSummaryAsync(
            organizerId,
            from: now.AddDays(-5),
            to: now.AddDays(-1));

        // Assert: Only bookingIn is matched
        Assert.Equal(1, result.TotalBookings);
        Assert.Equal(2, result.ActiveTicketsBooked);
    }

    [Fact]
    public async Task Scenario12_And_13_ExcludesPendingAndFailedPayments_NoDuplicateOrPrematureCounting()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo> { new() { Id = eventId, Title = "Test Event", OrganizerId = organizerId } });

        // 1 Confirmed
        var confirmed = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-CONF",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow.AddHours(-1),
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 2 } }
        };

        // 1 PendingPayment (Checkout in-flight)
        var pending = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-PEND",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.PendingPayment,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 4 } }
        };

        // 1 PaymentFailed (Card declined)
        var failed = new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-FAIL",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.PaymentFailed,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 1 } }
        };

        dbContext.Bookings.AddRange(confirmed, pending, failed);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetOrganizerBookingSummaryAsync(organizerId);

        // Assert: Only the confirmed booking is included
        Assert.Equal(1, result.TotalBookings);
        Assert.Equal(1, result.ConfirmedBookings);
        Assert.Equal(0, result.CancelledBookings);
        Assert.Equal(2, result.TotalTicketsBooked);
        Assert.Equal(2, result.ActiveTicketsBooked);
    }

    [Fact]
    public async Task Scenario14_OwnershipSecurity_AccessToAnotherOrganizersEvent_ReturnsNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var callerOrganizerId = Guid.NewGuid();
        var actualOwnerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        // Event belongs to actualOwnerId
        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Private Event",
                OrganizerId = actualOwnerId
            });

        // Act: caller tries to request event analytics
        var result = await service.GetEventBookingAnalyticsAsync(callerOrganizerId, eventId);

        // Assert: strictly returns null (non-disclosing 404)
        Assert.Null(result);
    }

    [Fact]
    public async Task Scenario15_GetOrganizerEventsBookingList_SupportsPaginationAndTitleSearch()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var ev1 = new EventSummaryInfo { Id = Guid.NewGuid(), Title = "Alpha Conference", OrganizerId = organizerId };
        var ev2 = new EventSummaryInfo { Id = Guid.NewGuid(), Title = "Beta Workshop", OrganizerId = organizerId };
        var ev3 = new EventSummaryInfo { Id = Guid.NewGuid(), Title = "Gamma Summit", OrganizerId = organizerId };

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo> { ev1, ev2, ev3 });

        // Add 1 booking to Alpha
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-ALPHA",
            EventId = ev1.Id,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 2 } }
        });

        await dbContext.SaveChangesAsync();

        // Act 1: Search for "Beta"
        var searchResult = await service.GetOrganizerEventsBookingListAsync(organizerId, search: "Beta");

        // Assert 1
        Assert.Single(searchResult.Items);
        Assert.Equal("Beta Workshop", searchResult.Items[0].EventName);

        // Act 2: Pagination (Page 1, PageSize 2)
        var pagedResult = await service.GetOrganizerEventsBookingListAsync(organizerId, page: 1, pageSize: 2);

        // Assert 2
        Assert.Equal(2, pagedResult.Items.Count);
        Assert.Equal(3, pagedResult.TotalCount);
        Assert.Equal(1, pagedResult.Page);
        Assert.Equal(2, pagedResult.PageSize);
    }
}
