using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using EventPulse.EventService.Controllers;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using Xunit;

namespace EventPulse.EventService.Tests;

/// <summary>
/// Focused tests verifying the past-event lifecycle rules across public discovery,
/// ticket visibility, inventory reservation, and organizer/internal historical access.
/// </summary>
public class EventPastLifecycleTests : IDisposable
{
    private readonly EventDbContext _context;

    public EventPastLifecycleTests()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EventDbContext(options);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static Event CreateTestEvent(
        string title,
        EventStatus status,
        DateTime eventDate,
        Guid? organizerId = null)
    {
        return new Event
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"Description for {title}",
            Venue = "Main Arena",
            EventDate = eventDate,
            Price = 50m,
            Category = "Music",
            VenueType = "Indoor",
            Status = status,
            OrganizerId = organizerId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static TicketType CreateTestTicketType(Guid eventId, int capacity = 100, int booked = 0)
    {
        return new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = "General Admission",
            Price = 50m,
            Capacity = capacity,
            BookedQuantity = booked,
            CreatedAt = DateTime.UtcNow
        };
    }

    // =========================================================================
    // Test A: Published future event appears in public discovery
    // =========================================================================
    [Fact]
    public async Task TestA_PublishedFutureEvent_AppearsInPublicDiscovery()
    {
        var now = DateTime.UtcNow;
        var timeProvider = new FixedTimeProvider(now);

        var futureEvent = CreateTestEvent("Future Rock Concert", EventStatus.Published, now.AddDays(7));
        _context.Events.Add(futureEvent);
        await _context.SaveChangesAsync();

        var controller = new EventsController(_context, null!, timeProvider: timeProvider);

        // 1. GET /api/events
        var listResult = await controller.GetEvents();
        var okList = Assert.IsType<OkObjectResult>(listResult.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okList.Value);
        Assert.Contains(items, e => e.Id == futureEvent.Id);

        // 2. GET /api/events/suggestions
        var suggResult = await controller.GetSuggestions("Rock");
        var okSugg = Assert.IsType<OkObjectResult>(suggResult.Result);
        var suggestions = Assert.IsAssignableFrom<IEnumerable<EventSuggestionDto>>(okSugg.Value);
        Assert.Contains(suggestions, s => s.Id == futureEvent.Id);
    }

    // =========================================================================
    // Test B: Published past event does NOT appear in public discovery
    // =========================================================================
    [Fact]
    public async Task TestB_PublishedPastEvent_DoesNotAppearInPublicDiscovery_AndReturnsNotFoundOnDetails()
    {
        var now = DateTime.UtcNow;
        var timeProvider = new FixedTimeProvider(now);

        var pastEvent = CreateTestEvent("Past Summer Festival", EventStatus.Published, now.AddDays(-5));
        var futureEvent = CreateTestEvent("Upcoming Autumn Gala", EventStatus.Published, now.AddDays(10));
        _context.Events.AddRange(pastEvent, futureEvent);
        await _context.SaveChangesAsync();

        var controller = new EventsController(_context, null!, timeProvider: timeProvider);

        // 1. GET /api/events should NOT list past event
        var listResult = await controller.GetEvents();
        var okList = Assert.IsType<OkObjectResult>(listResult.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okList.Value);
        Assert.DoesNotContain(items, e => e.Id == pastEvent.Id);
        Assert.Contains(items, e => e.Id == futureEvent.Id);

        // 2. GET /api/events/suggestions should NOT suggest past event
        var suggResult = await controller.GetSuggestions("Summer");
        var okSugg = Assert.IsType<OkObjectResult>(suggResult.Result);
        var suggestions = Assert.IsAssignableFrom<IEnumerable<EventSuggestionDto>>(okSugg.Value);
        Assert.DoesNotContain(suggestions, s => s.Id == pastEvent.Id);

        // 3. GET /api/events/{id} public details should return 404 NotFound
        var detailsResult = await controller.GetEventById(pastEvent.Id.ToString());
        Assert.IsType<NotFoundResult>(detailsResult.Result);
    }

    // =========================================================================
    // Test C: Past published event cannot expose purchasable ticket data
    // =========================================================================
    [Fact]
    public async Task TestC_PastPublishedEvent_CannotExposePurchasableTicketData()
    {
        var now = DateTime.UtcNow;
        var timeProvider = new FixedTimeProvider(now);

        var pastEvent = CreateTestEvent("Concluded Expo", EventStatus.Published, now.AddHours(-3));
        var ticketType = CreateTestTicketType(pastEvent.Id, capacity: 50);
        _context.Events.Add(pastEvent);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        var ticketTypeService = new TicketTypeService(_context, NullLogger<TicketTypeService>.Instance, timeProvider);

        // Public ticket retrieval should return (null, isNotFound: true)
        var (tickets, isNotFound) = await ticketTypeService.GetPublicByEventIdAsync(pastEvent.Id);

        Assert.True(isNotFound, "Past event should result in not-found flag for public ticket queries.");
        Assert.Null(tickets);
    }

    // =========================================================================
    // Test D: Past published event cannot reserve/decrement inventory / accept new booking activity
    // =========================================================================
    [Fact]
    public async Task TestD_PastPublishedEvent_CannotReserveInventory_ReturnsBadRequest()
    {
        var now = DateTime.UtcNow;
        var timeProvider = new FixedTimeProvider(now);

        var pastEvent = CreateTestEvent("Yesterday's Play", EventStatus.Published, now.AddDays(-1));
        var ticketType = CreateTestTicketType(pastEvent.Id, capacity: 10, booked: 0);
        _context.Events.Add(pastEvent);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        var inventoryService = new EventInventoryService(_context, NullLogger<EventInventoryService>.Instance, timeProvider);

        var request = new ReserveInventoryRequest
        {
            BookingId = Guid.NewGuid(),
            Items = new List<TicketReservationItemDto>
            {
                new() { TicketTypeId = ticketType.Id, Quantity = 2 }
            }
        };

        var result = await inventoryService.ReserveInventoryAsync(pastEvent.Id, request);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Event is no longer open for ticket purchase.", result.ErrorMessage);

        // Confirm inventory was NOT decremented / booked quantity did NOT change
        var refreshedTicketType = await _context.TicketTypes.FindAsync(ticketType.Id);
        Assert.NotNull(refreshedTicketType);
        Assert.Equal(0, refreshedTicketType.BookedQuantity);
    }

    // =========================================================================
    // Test E: Future published event continues working normally
    // =========================================================================
    [Fact]
    public async Task TestE_FuturePublishedEvent_ContinuesWorkingNormallyAcrossAllPaths()
    {
        var now = DateTime.UtcNow;
        var timeProvider = new FixedTimeProvider(now);

        var futureEvent = CreateTestEvent("Grand Gala 2026", EventStatus.Published, now.AddDays(14));
        var ticketType = CreateTestTicketType(futureEvent.Id, capacity: 20, booked: 0);
        _context.Events.Add(futureEvent);
        _context.TicketTypes.Add(ticketType);
        await _context.SaveChangesAsync();

        var controller = new EventsController(_context, null!, timeProvider: timeProvider);
        var ticketTypeService = new TicketTypeService(_context, NullLogger<TicketTypeService>.Instance, timeProvider);
        var inventoryService = new EventInventoryService(_context, NullLogger<EventInventoryService>.Instance, timeProvider);

        // 1. Discovery lists it
        var listResult = await controller.GetEvents();
        var okList = Assert.IsType<OkObjectResult>(listResult.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<EventListDto>>(okList.Value);
        Assert.Contains(items, e => e.Id == futureEvent.Id);

        // 2. Public details resolves it
        var detailsResult = await controller.GetEventById(futureEvent.Id.ToString());
        var okDetails = Assert.IsType<OkObjectResult>(detailsResult.Result);
        var details = Assert.IsType<EventDetailsDto>(okDetails.Value);
        Assert.Equal(futureEvent.Id, details.Id);

        // 3. Public ticket types resolves them
        var (tickets, isNotFound) = await ticketTypeService.GetPublicByEventIdAsync(futureEvent.Id);
        Assert.False(isNotFound);
        Assert.NotNull(tickets);
        Assert.Single(tickets);

        // 4. Inventory reservation succeeds
        var bookingId = Guid.NewGuid();
        var request = new ReserveInventoryRequest
        {
            BookingId = bookingId,
            Items = new List<TicketReservationItemDto>
            {
                new() { TicketTypeId = ticketType.Id, Quantity = 3 }
            }
        };

        var reserveResult = await inventoryService.ReserveInventoryAsync(futureEvent.Id, request);
        Assert.True(reserveResult.Success);
        Assert.Equal(200, reserveResult.StatusCode);

        // 5. Active reservation hold created
        var reservation = await _context.InventoryReservations.FirstOrDefaultAsync(r => r.BookingId == bookingId);
        Assert.NotNull(reservation);
        Assert.Equal(3, reservation.Quantity);
        Assert.Equal(InventoryReservationStatus.Active, reservation.Status);

        // 6. Commit reservation updates booked quantity
        var commitResult = await inventoryService.CommitInventoryAsync(futureEvent.Id, new CommitInventoryRequest { BookingId = bookingId });
        Assert.True(commitResult.Success);

        var refreshedTicketType = await _context.TicketTypes.FindAsync(ticketType.Id);
        Assert.NotNull(refreshedTicketType);
        Assert.Equal(3, refreshedTicketType.BookedQuantity);
    }

    // =========================================================================
    // Test F: Existing organizer/admin historical behavior is not broken
    // =========================================================================
    [Fact]
    public async Task TestF_ExistingOrganizerAndInternalHistoricalBehavior_IsNotBroken()
    {
        var now = DateTime.UtcNow;
        var timeProvider = new FixedTimeProvider(now);
        var organizerId = Guid.NewGuid();

        var pastPublished = CreateTestEvent("Past Finished Event", EventStatus.Published, now.AddMonths(-2), organizerId);
        _context.Events.Add(pastPublished);
        await _context.SaveChangesAsync();

        var controller = new EventsController(_context, null!, timeProvider: timeProvider);

        // 1. Historical database record is retained (NOT deleted)
        var dbRecord = await _context.Events.FindAsync(pastPublished.Id);
        Assert.NotNull(dbRecord);
        Assert.Equal(EventStatus.Published, dbRecord.Status);

        // 2. Internal service lookup GET /api/events/internal/{id} resolves past event details
        var internalDetails = await controller.GetEventInternalById(pastPublished.Id.ToString());
        var okInternal = Assert.IsType<OkObjectResult>(internalDetails.Result);
        var internalDto = Assert.IsType<EventDetailsDto>(okInternal.Value);
        Assert.Equal(pastPublished.Id, internalDto.Id);

        // 3. Organizer internal lookup GET /api/events/internal/organizer/{organizerId} includes past events
        var organizerEventsResult = await controller.GetEventsByOrganizerInternal(organizerId);
        var okOrganizer = Assert.IsType<OkObjectResult>(organizerEventsResult.Result);
        var organizerEvents = Assert.IsAssignableFrom<IReadOnlyList<EventDetailsDto>>(okOrganizer.Value);
        Assert.Contains(organizerEvents, e => e.Id == pastPublished.Id);
    }
}
