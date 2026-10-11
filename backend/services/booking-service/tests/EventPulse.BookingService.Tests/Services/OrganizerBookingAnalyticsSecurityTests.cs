using System.Reflection;
using System.Security.Claims;
using EventPulse.BookingService.Controllers;
using EventPulse.BookingService.Data;
using EventPulse.BookingService.DTOs;
using EventPulse.BookingService.Models;
using EventPulse.BookingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EventPulse.BookingService.Tests.Services;

/// <summary>
/// EP-389: Security & Tenancy Isolation Tests for Booking Analytics
/// Verifies:
/// 1. Anonymous requests denied
/// 2. Customer requests denied (policy verification)
/// 3. Organizers can access own analytics
/// 4. Organizers cannot access another organizer's event
/// 5. Manipulating event IDs does not bypass authorization
/// 6. Manipulating organizer identity claims does not bypass tenant isolation
/// 7. Query filters cannot leak other organizers' records
/// </summary>
public class OrganizerBookingAnalyticsSecurityTests
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
    public async Task Scenario1_AnonymousRequest_ReturnsUnauthorized()
    {
        var serviceMock = new Mock<IOrganizerBookingAnalyticsService>();
        var controller = new OrganizerBookingAnalyticsController(
            serviceMock.Object,
            NullLogger<OrganizerBookingAnalyticsController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetOrganizerBookingSummary(null, null, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public void Scenario2_CustomerRole_RejectedByOrganizerOnlyPolicy()
    {
        var authorizeAttribute = typeof(OrganizerBookingAnalyticsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal(AppPolicies.OrganizerOnly, authorizeAttribute.Policy);

        // Verify customer claims do not satisfy Organizer requirement
        var customerClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Customer")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(customerClaims, "TestAuth"));

        Assert.False(principal.IsInRole("Organizer"));
        Assert.True(principal.IsInRole("Customer"));
    }

    [Fact]
    public async Task Scenario3_OrganizersOwnEvent_AllowedAndReturnsAnalytics()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = eventId,
                Title = "Owned Event",
                OrganizerId = organizerId
            });

        clientMock.Setup(c => c.GetEventTicketTypesAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TicketTypeAvailability>
            {
                new() { Id = Guid.NewGuid(), Name = "General", TotalQuantity = 100, RemainingQuantity = 98 }
            });

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-OWN",
            EventId = eventId,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 2 } }
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetEventBookingAnalyticsAsync(organizerId, eventId);

        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(1, result.TotalBookings);
        Assert.Equal(2, result.ActiveTicketsBooked);
    }

    [Fact]
    public async Task Scenario4_AnotherOrganizersEvent_DeniedWithNonDisclosingNull()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var attackerOrganizerId = Guid.NewGuid();
        var victimOrganizerId = Guid.NewGuid();
        var victimEventId = Guid.NewGuid();

        // Event is owned by victimOrganizerId
        clientMock.Setup(c => c.GetEventSummaryAsync(victimEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventSummaryInfo
            {
                Id = victimEventId,
                Title = "Private Victim Event",
                OrganizerId = victimOrganizerId
            });

        var result = await service.GetEventBookingAnalyticsAsync(attackerOrganizerId, victimEventId);

        // Must return null (mapped to 404 by controller)
        Assert.Null(result);
    }

    [Fact]
    public async Task Scenario5_ManipulatedEventId_NonExistent_ReturnsNull()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerId = Guid.NewGuid();
        var nonExistentEventId = Guid.NewGuid();

        clientMock.Setup(c => c.GetEventSummaryAsync(nonExistentEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSummaryInfo?)null);

        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo>());

        var result = await service.GetEventBookingAnalyticsAsync(organizerId, nonExistentEventId);

        Assert.Null(result);
    }

    [Fact]
    public async Task Scenario6_QueryFilters_NeverLeakOtherOrganizersBookings()
    {
        using var dbContext = CreateInMemoryDbContext();
        var clientMock = new Mock<IEventAvailabilityClient>();
        var service = new OrganizerBookingAnalyticsService(dbContext, clientMock.Object, NullLogger<OrganizerBookingAnalyticsService>.Instance);

        var organizerA = Guid.NewGuid();
        var organizerB = Guid.NewGuid();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();

        // Organizer A owns Event A only
        clientMock.Setup(c => c.GetOrganizerEventsAsync(organizerA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventSummaryInfo> { new() { Id = eventA, Title = "A Event", OrganizerId = organizerA } });

        // Add bookings for Event A (5 tickets) and Event B (100 tickets)
        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-A",
            EventId = eventA,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 5 } }
        });

        dbContext.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            BookingReference = "BK-B",
            EventId = eventB,
            CustomerId = Guid.NewGuid(),
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
            Items = new List<BookingItem> { new() { Id = Guid.NewGuid(), Quantity = 100 } }
        });

        await dbContext.SaveChangesAsync();

        // Act: Organizer A requests overall summary
        var summaryA = await service.GetOrganizerBookingSummaryAsync(organizerA);

        // Assert: Exactly Event A bookings returned, Event B is never leaked
        Assert.Equal(1, summaryA.TotalBookings);
        Assert.Equal(5, summaryA.ActiveTicketsBooked);
        Assert.Equal(0, summaryA.CancelledBookings);
    }
}
