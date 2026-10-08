using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Services;
using Xunit;

namespace EventPulse.EventService.Tests;

public class EventInventoryServiceTests : IDisposable
{
    private readonly EventDbContext _context;
    private readonly EventInventoryService _inventoryService;
    private readonly Guid _eventId;
    private readonly Guid _ticketTypeId;

    public EventInventoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new EventDbContext(options);
        _inventoryService = new EventInventoryService(_context, NullLogger<EventInventoryService>.Instance);

        _eventId = Guid.NewGuid();
        _ticketTypeId = Guid.NewGuid();

        // Seed Published Event with 10 tickets
        var eventItem = new Event
        {
            Id = _eventId,
            Title = "Tech Summit 2026",
            Description = "Annual developer conference with world-class speakers and hands-on workshops.",
            Venue = "Colombo Convention Center",
            EventDate = DateTime.UtcNow.AddDays(30),
            Price = 1000m,
            Status = EventStatus.Published,
            OrganizerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = _ticketTypeId,
            EventId = _eventId,
            Name = "General Admission",
            Price = 1000m,
            Capacity = 10,
            BookedQuantity = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventItem);
        _context.TicketTypes.Add(ticketType);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task Reserve_WhenAvailable_SucceedsAndCreatesReservation()
    {
        var bookingId = Guid.NewGuid();
        var request = new ReserveInventoryRequest
        {
            BookingId = bookingId,
            Items = new List<TicketReservationItemDto>
            {
                new() { TicketTypeId = _ticketTypeId, Quantity = 4 }
            }
        };

        var result = await _inventoryService.ReserveInventoryAsync(_eventId, request);

        Assert.True(result.Success);
        var reservation = await _context.InventoryReservations.FirstOrDefaultAsync(r => r.BookingId == bookingId);
        Assert.NotNull(reservation);
        Assert.Equal(4, reservation.Quantity);
        Assert.Equal(InventoryReservationStatus.Active, reservation.Status);
    }

    [Fact]
    public async Task Reserve_WhenExceedsAvailable_FailsWithConflict()
    {
        var bookingId = Guid.NewGuid();
        var request = new ReserveInventoryRequest
        {
            BookingId = bookingId,
            Items = new List<TicketReservationItemDto>
            {
                new() { TicketTypeId = _ticketTypeId, Quantity = 15 } // Capacity is 10
            }
        };

        var result = await _inventoryService.ReserveInventoryAsync(_eventId, request);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("Insufficient tickets", result.ErrorMessage);
    }

    [Fact]
    public async Task ConcurrentPurchases_CompetingForLastTicket_OnlyOneWins()
    {
        // 1 ticket remaining
        var tt = await _context.TicketTypes.FindAsync(_ticketTypeId);
        tt!.BookedQuantity = 9; // Capacity 10 => 1 remaining
        await _context.SaveChangesAsync();

        var booking1 = Guid.NewGuid();
        var booking2 = Guid.NewGuid();

        var req1 = new ReserveInventoryRequest
        {
            BookingId = booking1,
            Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 1 } }
        };
        var req2 = new ReserveInventoryRequest
        {
            BookingId = booking2,
            Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 1 } }
        };

        var task1 = _inventoryService.ReserveInventoryAsync(_eventId, req1);
        var task2 = _inventoryService.ReserveInventoryAsync(_eventId, req2);

        var results = await Task.WhenAll(task1, task2);

        var succeededCount = results.Count(r => r.Success);
        var failedCount = results.Count(r => !r.Success && r.StatusCode == 409);

        Assert.Equal(1, succeededCount);
        Assert.Equal(1, failedCount);
    }

    [Fact]
    public async Task Commit_IncrementsBookedQuantity_AndIsIdempotent()
    {
        var bookingId = Guid.NewGuid();
        var reserveReq = new ReserveInventoryRequest
        {
            BookingId = bookingId,
            Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 3 } }
        };
        await _inventoryService.ReserveInventoryAsync(_eventId, reserveReq);

        var commitReq = new CommitInventoryRequest { BookingId = bookingId };
        var commitResult = await _inventoryService.CommitInventoryAsync(_eventId, commitReq);

        Assert.True(commitResult.Success);
        var tt = await _context.TicketTypes.FindAsync(_ticketTypeId);
        Assert.Equal(3, tt!.BookedQuantity);

        // Call commit again (idempotency test)
        var secondCommitResult = await _inventoryService.CommitInventoryAsync(_eventId, commitReq);
        Assert.True(secondCommitResult.Success);
        // Quantity should still be 3, NOT 6!
        Assert.Equal(3, tt.BookedQuantity);
    }

    [Fact]
    public async Task Release_MarksReservationReleased_MakingStockAvailableAgain()
    {
        var bookingId = Guid.NewGuid();
        var reserveReq = new ReserveInventoryRequest
        {
            BookingId = bookingId,
            Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 10 } }
        };
        await _inventoryService.ReserveInventoryAsync(_eventId, reserveReq);

        // Another buyer tries to reserve while hold is active => should fail
        var otherBookingId = Guid.NewGuid();
        var otherReq = new ReserveInventoryRequest
        {
            BookingId = otherBookingId,
            Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 1 } }
        };
        var failResult = await _inventoryService.ReserveInventoryAsync(_eventId, otherReq);
        Assert.False(failResult.Success);

        // Now release booking 1
        var releaseResult = await _inventoryService.ReleaseInventoryAsync(_eventId, new ReleaseInventoryRequest { BookingId = bookingId });
        Assert.True(releaseResult.Success);

        // Now buyer 2 can reserve
        var retryResult = await _inventoryService.ReserveInventoryAsync(_eventId, otherReq);
        Assert.True(retryResult.Success);
    }

    [Fact]
    public async Task Restore_DecrementsBookedQuantity_AndIsIdempotentOnCancellationKey()
    {
        var bookingId = Guid.NewGuid();
        var reserveReq = new ReserveInventoryRequest
        {
            BookingId = bookingId,
            Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 5 } }
        };
        await _inventoryService.ReserveInventoryAsync(_eventId, reserveReq);
        await _inventoryService.CommitInventoryAsync(_eventId, new CommitInventoryRequest { BookingId = bookingId });

        var tt = await _context.TicketTypes.FindAsync(_ticketTypeId);
        Assert.Equal(5, tt!.BookedQuantity);

        // Restore cancellation
        var restoreReq = new RestoreInventoryRequest
        {
            BookingId = bookingId,
            CancellationKey = $"cancel-{bookingId}",
            Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 5 } }
        };

        var restoreResult = await _inventoryService.RestoreInventoryAsync(_eventId, restoreReq);
        Assert.True(restoreResult.Success);

        tt = await _context.TicketTypes.FindAsync(_ticketTypeId);
        Assert.Equal(0, tt!.BookedQuantity);

        // Second call with same cancellation key (duplicate webhook/Kafka event)
        var secondRestoreResult = await _inventoryService.RestoreInventoryAsync(_eventId, restoreReq);
        Assert.True(secondRestoreResult.Success);

        // BookedQuantity must never drop below 0 or double decrement
        Assert.Equal(0, tt.BookedQuantity);
    }

    [Fact]
    public async Task Restore_NeverExceedsCapacity_OrDropsBelowZero()
    {
        var bookingId = Guid.NewGuid();
        var restoreReq = new RestoreInventoryRequest
        {
            BookingId = bookingId,
            CancellationKey = "key-oversize-1",
            Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 999 } }
        };

        var result = await _inventoryService.RestoreInventoryAsync(_eventId, restoreReq);
        Assert.True(result.Success);

        var tt = await _context.TicketTypes.FindAsync(_ticketTypeId);
        Assert.Equal(0, tt!.BookedQuantity); // clamped at 0
        Assert.True(tt.RemainingQuantity <= tt.Capacity);
    }
}
