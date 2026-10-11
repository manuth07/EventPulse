using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Services;

public class EventInventoryService : IEventInventoryService
{
    private readonly EventDbContext _context;
    private readonly ILogger<EventInventoryService>? _logger;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> KeyLocks = new();

    public EventInventoryService(EventDbContext context, ILogger<EventInventoryService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    private static SemaphoreSlim GetLock(Guid eventId) =>
        KeyLocks.GetOrAdd(eventId, _ => new SemaphoreSlim(1, 1));

    public async Task<InventoryOperationResult> ReserveInventoryAsync(
        Guid eventId,
        ReserveInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BookingId == Guid.Empty)
            return InventoryOperationResult.Failed("BookingId is required.", 400);

        if (request.Items == null || request.Items.Count == 0)
            return InventoryOperationResult.Failed("At least one ticket item is required.", 400);

        if (request.Items.Any(i => i.Quantity <= 0))
            return InventoryOperationResult.Failed("Item quantities must be greater than zero.", 400);

        var sem = GetLock(eventId);
        await sem.WaitAsync(cancellationToken);
        try
        {
            var eventItem = await _context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

            if (eventItem == null)
                return InventoryOperationResult.Failed("Event not found.", 404);

            if (eventItem.Status != EventStatus.Published)
                return InventoryOperationResult.Failed($"Event is not open for ticket purchase (Status: {eventItem.Status}).", 400);

            // Block if event has pending cancellation request
            var hasPendingCancellation = await _context.EventCancellationRequests
                .AnyAsync(r => r.EventId == eventId && r.Status == EventCancellationRequestStatus.Pending, cancellationToken);
            if (hasPendingCancellation)
                return InventoryOperationResult.Failed("Event ticket sales are currently paused due to a pending cancellation request.", 409);

            var existingReservations = await _context.InventoryReservations
                .Where(r => r.BookingId == request.BookingId && r.Status == InventoryReservationStatus.Active)
                .ToListAsync(cancellationToken);

            if (existingReservations.Count > 0)
            {
                // Already reserved for this booking
                return InventoryOperationResult.Succeeded();
            }

            var ticketTypeIds = request.Items.Select(i => i.TicketTypeId).Distinct().ToList();
            var ticketTypes = await _context.TicketTypes
                .Where(t => t.EventId == eventId && ticketTypeIds.Contains(t.Id))
                .ToListAsync(cancellationToken);

            if (ticketTypes.Count != ticketTypeIds.Count)
                return InventoryOperationResult.Failed("One or more ticket types not found for this event.", 404);

            var now = DateTimeOffset.UtcNow;

            // Fetch active unexpired reservations for these ticket types
            var activeHolds = await _context.InventoryReservations
                .Where(r => r.EventId == eventId &&
                            ticketTypeIds.Contains(r.TicketTypeId) &&
                            r.Status == InventoryReservationStatus.Active &&
                            r.ExpiresAtUtc > now)
                .GroupBy(r => r.TicketTypeId)
                .Select(g => new { TicketTypeId = g.Key, ReservedQty = g.Sum(r => r.Quantity) })
                .ToDictionaryAsync(g => g.TicketTypeId, g => g.ReservedQty, cancellationToken);

            // Check availability for each requested item
            foreach (var item in request.Items)
            {
                var tt = ticketTypes.First(t => t.Id == item.TicketTypeId);
                var activeHoldQty = activeHolds.GetValueOrDefault(item.TicketTypeId, 0);
                var remaining = Math.Clamp(tt.Capacity - tt.BookedQuantity, 0, tt.Capacity);
                var available = Math.Max(0, remaining - activeHoldQty);

                if (available < item.Quantity)
                {
                    _logger?.LogWarning(
                        "Insufficient inventory for TicketType {TicketTypeId}. Requested={Req}, Available={Avail}, Capacity={Cap}, Booked={Booked}, Reserved={Res}",
                        tt.Id, item.Quantity, available, tt.Capacity, tt.BookedQuantity, activeHoldQty);
                    return InventoryOperationResult.Failed(
                        $"Insufficient tickets available for '{tt.Name}'. Requested {item.Quantity}, only {available} available.", 409);
                }
            }

            // Create reservations
            var holdMinutes = request.HoldDurationMinutes > 0 ? request.HoldDurationMinutes : 15;
            var expiresAt = now.AddMinutes(holdMinutes);

            foreach (var item in request.Items)
            {
                var reservation = new InventoryReservation
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    BookingId = request.BookingId,
                    TicketTypeId = item.TicketTypeId,
                    Quantity = item.Quantity,
                    Status = InventoryReservationStatus.Active,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = expiresAt
                };
                _context.InventoryReservations.Add(reservation);
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Inventory reserved successfully for Booking {BookingId}, Event {EventId}", request.BookingId, eventId);
            return InventoryOperationResult.Succeeded();
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<InventoryOperationResult> CommitInventoryAsync(
        Guid eventId,
        CommitInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BookingId == Guid.Empty)
            return InventoryOperationResult.Failed("BookingId is required.", 400);

        var sem = GetLock(eventId);
        await sem.WaitAsync(cancellationToken);
        try
        {
            var reservations = await _context.InventoryReservations
                .Where(r => r.BookingId == request.BookingId)
                .ToListAsync(cancellationToken);

            if (reservations.Count == 0)
            {
                _logger?.LogWarning("No reservation found to commit for Booking {BookingId}", request.BookingId);
                return InventoryOperationResult.Failed($"No reservation found for Booking {request.BookingId}.", 404);
            }

            // If already committed, return success (idempotent)
            if (reservations.All(r => r.Status == InventoryReservationStatus.Committed))
            {
                _logger?.LogInformation("Reservation for Booking {BookingId} is already committed (idempotent).", request.BookingId);
                return InventoryOperationResult.Succeeded();
            }

            var ticketTypeIds = reservations.Select(r => r.TicketTypeId).Distinct().ToList();
            var ticketTypes = await _context.TicketTypes
                .Where(t => t.EventId == eventId && ticketTypeIds.Contains(t.Id))
                .ToListAsync(cancellationToken);

            foreach (var res in reservations)
            {
                if (res.Status == InventoryReservationStatus.Active)
                {
                    var tt = ticketTypes.FirstOrDefault(t => t.Id == res.TicketTypeId);
                    if (tt != null)
                    {
                        // Commit: increment BookedQuantity (bounded by Capacity)
                        tt.BookedQuantity = Math.Min(tt.Capacity, tt.BookedQuantity + res.Quantity);
                    }
                    res.Status = InventoryReservationStatus.Committed;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Inventory successfully committed for Booking {BookingId}", request.BookingId);
            return InventoryOperationResult.Succeeded();
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<InventoryOperationResult> ReleaseInventoryAsync(
        Guid eventId,
        ReleaseInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BookingId == Guid.Empty)
            return InventoryOperationResult.Failed("BookingId is required.", 400);

        var sem = GetLock(eventId);
        await sem.WaitAsync(cancellationToken);
        try
        {
            var reservations = await _context.InventoryReservations
                .Where(r => r.BookingId == request.BookingId && r.Status == InventoryReservationStatus.Active)
                .ToListAsync(cancellationToken);

            if (reservations.Count == 0)
            {
                // Either already released or not found; idempotent
                return InventoryOperationResult.Succeeded();
            }

            foreach (var res in reservations)
            {
                res.Status = InventoryReservationStatus.Released;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Inventory reservation released for Booking {BookingId}", request.BookingId);
            return InventoryOperationResult.Succeeded();
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<InventoryOperationResult> RestoreInventoryAsync(
        Guid eventId,
        RestoreInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BookingId == Guid.Empty)
            return InventoryOperationResult.Failed("BookingId is required.", 400);

        if (string.IsNullOrWhiteSpace(request.CancellationKey))
            return InventoryOperationResult.Failed("CancellationKey is required for idempotency.", 400);

        if (request.Items == null || request.Items.Count == 0)
            return InventoryOperationResult.Failed("At least one ticket item is required to restore.", 400);

        var sem = GetLock(eventId);
        await sem.WaitAsync(cancellationToken);
        try
        {
            // Check idempotency: if this CancellationKey was already processed, return success
            var alreadyRestored = await _context.InventoryRestorations
                .AnyAsync(r => r.CancellationKey == request.CancellationKey, cancellationToken);

            if (alreadyRestored)
            {
                _logger?.LogInformation("Inventory restoration for key {Key} already processed (idempotent).", request.CancellationKey);
                return InventoryOperationResult.Succeeded();
            }

            var ticketTypeIds = request.Items.Select(i => i.TicketTypeId).Distinct().ToList();
            var ticketTypes = await _context.TicketTypes
                .Where(t => t.EventId == eventId && ticketTypeIds.Contains(t.Id))
                .ToListAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;

            foreach (var item in request.Items)
            {
                var tt = ticketTypes.FirstOrDefault(t => t.Id == item.TicketTypeId);
                if (tt != null)
                {
                    // Restore: decrement BookedQuantity (never drop below 0)
                    tt.BookedQuantity = Math.Max(0, tt.BookedQuantity - item.Quantity);
                }

                var restorationRecord = new InventoryRestoration
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    BookingId = request.BookingId,
                    TicketId = request.TicketId,
                    CancellationKey = request.CancellationKey,
                    TicketTypeId = item.TicketTypeId,
                    RestoredQuantity = item.Quantity,
                    RestoredAtUtc = now
                };
                _context.InventoryRestorations.Add(restorationRecord);
            }

            // Also mark any committed reservations for this booking as Released if full cancellation
            // (or leave as is if partial)
            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation(
                "Inventory restored for Booking {BookingId}, CancellationKey {Key}",
                request.BookingId, request.CancellationKey);

            return InventoryOperationResult.Succeeded();
        }
        finally
        {
            sem.Release();
        }
    }
}
