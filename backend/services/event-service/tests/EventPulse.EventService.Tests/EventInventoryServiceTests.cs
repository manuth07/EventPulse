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

    [Fact]
    public async Task Restore_MultipleTicketTypes_RestoresBothAndRecordsIdempotentRecords()
    {
        // Seed a second ticket type (VIP)
        var vipTicketTypeId = Guid.NewGuid();
        var vipTicketType = new TicketType
        {
            Id = vipTicketTypeId,
            EventId = _eventId,
            Name = "VIP Pass",
            Price = 2500m,
            Capacity = 5,
            BookedQuantity = 5,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(vipTicketType);

        var general = await _context.TicketTypes.FindAsync(_ticketTypeId);
        general!.BookedQuantity = 8;
        await _context.SaveChangesAsync();

        var bookingId = Guid.NewGuid();
        var cancellationKey = $"booking-{bookingId}-cancelled";
        var restoreReq = new RestoreInventoryRequest
        {
            BookingId = bookingId,
            CancellationKey = cancellationKey,
            Items = new List<TicketReservationItemDto>
            {
                new() { TicketTypeId = _ticketTypeId, Quantity = 3 },
                new() { TicketTypeId = vipTicketTypeId, Quantity = 2 }
            }
        };

        var result = await _inventoryService.RestoreInventoryAsync(_eventId, restoreReq);

        Assert.True(result.Success);
        var updatedGeneral = await _context.TicketTypes.FindAsync(_ticketTypeId);
        var updatedVip = await _context.TicketTypes.FindAsync(vipTicketTypeId);

        Assert.Equal(5, updatedGeneral!.BookedQuantity); // 8 - 3 = 5
        Assert.Equal(3, updatedVip!.BookedQuantity);     // 5 - 2 = 3

        // Verify restoration records created without unique key conflicts
        var restorations = await _context.InventoryRestorations
            .Where(r => r.BookingId == bookingId)
            .ToListAsync();
        Assert.Equal(2, restorations.Count);

        // Second call with same key is idempotent and does not restore again
        var secondResult = await _inventoryService.RestoreInventoryAsync(_eventId, restoreReq);
        Assert.True(secondResult.Success);
        Assert.Equal(5, updatedGeneral.BookedQuantity);
        Assert.Equal(3, updatedVip.BookedQuantity);
    }

    [Fact]
    public async Task Restore_WhenEventInPast_RejectsWithPolicyGuard()
    {
        // Set event date in the past
        var pastEventId = Guid.NewGuid();
        var pastTicketTypeId = Guid.NewGuid();
        var pastEvent = new Event
        {
            Id = pastEventId,
            Title = "Concluded Concert",
            Description = "Event that already ended yesterday",
            Venue = "Past Arena",
            EventDate = DateTime.UtcNow.AddDays(-2),
            Price = 500m,
            Status = EventStatus.Published,
            OrganizerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        var pastTicketType = new TicketType
        {
            Id = pastTicketTypeId,
            EventId = pastEventId,
            Name = "Past Ticket",
            Price = 500m,
            Capacity = 20,
            BookedQuantity = 10,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        _context.Events.Add(pastEvent);
        _context.TicketTypes.Add(pastTicketType);
        await _context.SaveChangesAsync();

        var restoreReq = new RestoreInventoryRequest
        {
            BookingId = Guid.NewGuid(),
            CancellationKey = "key-past-event-1",
            Items = new List<TicketReservationItemDto>
            {
                new() { TicketTypeId = pastTicketTypeId, Quantity = 5 }
            }
        };

        var result = await _inventoryService.RestoreInventoryAsync(pastEventId, restoreReq);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("already started or concluded", result.ErrorMessage);

        // Verify booked quantity was untouched
        var tt = await _context.TicketTypes.FindAsync(pastTicketTypeId);
        Assert.Equal(10, tt!.BookedQuantity);
    }

    [Fact]
    public async Task Restore_UpdatesCustomerAvailability_ReflectedInTicketTypeService()
    {
        var tt = await _context.TicketTypes.FindAsync(_ticketTypeId);
        tt!.BookedQuantity = 10; // Completely sold out (Capacity = 10)
        await _context.SaveChangesAsync();

        var ticketTypeService = new TicketTypeService(_context);

        // Before restoration: customer sees 0 remaining and sold out
        var (beforeList, _) = await ticketTypeService.GetPublicByEventIdAsync(_eventId);
        var beforeTicket = beforeList!.First(t => t.Id == _ticketTypeId);
        Assert.Equal(0, beforeTicket.RemainingQuantity);
        Assert.True(beforeTicket.IsSoldOut);

        // Cancel booking and restore 4 tickets
        var bookingId = Guid.NewGuid();
        var restoreReq = new RestoreInventoryRequest
        {
            BookingId = bookingId,
            CancellationKey = $"cancel-avail-{bookingId}",
            Items = new List<TicketReservationItemDto>
            {
                new() { TicketTypeId = _ticketTypeId, Quantity = 4 }
            }
        };
        var restoreResult = await _inventoryService.RestoreInventoryAsync(_eventId, restoreReq);
        Assert.True(restoreResult.Success);

        // After restoration: customer sees updated availability and IsSoldOut is false
        var (afterList, _) = await ticketTypeService.GetPublicByEventIdAsync(_eventId);
        var afterTicket = afterList!.First(t => t.Id == _ticketTypeId);
        Assert.Equal(4, afterTicket.RemainingQuantity);
        Assert.Equal(4, afterTicket.AvailableQuantity);
        Assert.False(afterTicket.IsSoldOut);
    }

    [Fact]
    public async Task Restore_ConcurrentPurchaseAndRestoration_MaintainsInventoryIntegrity()
    {
        var tt = await _context.TicketTypes.FindAsync(_ticketTypeId);
        tt!.BookedQuantity = 5; // 5 out of 10 booked
        await _context.SaveChangesAsync();

        var bookingA = Guid.NewGuid();
        var bookingB = Guid.NewGuid();

        // Concurrently reserve 3 tickets for customer A and restore 2 tickets from cancelled customer B
        var reserveTask = Task.Run(async () =>
        {
            using var scopeContext = new EventDbContext(new DbContextOptionsBuilder<EventDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);
            return await _inventoryService.ReserveInventoryAsync(_eventId, new ReserveInventoryRequest
            {
                BookingId = bookingA,
                Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 2 } }
            });
        });

        var restoreTask = Task.Run(async () =>
        {
            return await _inventoryService.RestoreInventoryAsync(_eventId, new RestoreInventoryRequest
            {
                BookingId = bookingB,
                CancellationKey = $"cancel-{bookingB}",
                Items = new List<TicketReservationItemDto> { new() { TicketTypeId = _ticketTypeId, Quantity = 2 } }
            });
        });

        await Task.WhenAll(reserveTask, restoreTask);

        var finalTt = await _context.TicketTypes.FindAsync(_ticketTypeId);
        Assert.True(finalTt!.BookedQuantity >= 0 && finalTt.BookedQuantity <= finalTt.Capacity);
    }
}
