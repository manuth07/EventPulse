using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Services;

/// <summary>
/// Implements US-19 — Create Ticket Types and US-20 — Update Ticket Types.
/// Eligibility rule: an event must be owned by the requesting organizer and in
/// Approved or Published status. Pending/Rejected events cannot have ticket types.
/// </summary>
public class TicketTypeService : ITicketTypeService
{
    private readonly EventDbContext _context;
    private readonly ILogger<TicketTypeService>? _logger;

    private static readonly HashSet<EventStatus> EligibleStatuses = new()
    {
        EventStatus.Approved,
        EventStatus.Published,
    };

    public TicketTypeService(EventDbContext context, ILogger<TicketTypeService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(TicketTypeDto? Result, string? Error, bool IsNotFound, bool IsForbidden, bool IsInvalidState)> CreateAsync(
        Guid eventId,
        CreateTicketTypeRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (eventItem == null)
        {
            return (null, "Event not found.", true, false, false);
        }

        if (eventItem.OrganizerId != organizerId)
        {
            _logger?.LogWarning(
                "CreateTicketType forbidden: Event {EventId} belongs to Organizer {ActualOrganizer}, request by {RequesterId}",
                eventId, eventItem.OrganizerId, organizerId);
            return (null, "You do not have permission to add ticket types to this event.", false, true, false);
        }

        if (!EligibleStatuses.Contains(eventItem.Status))
        {
            return (null,
                $"Ticket types can only be added to Approved or Published events. Current status: {eventItem.Status}.",
                false, false, true);
        }

        // ---- Domain Validation ------------------------------------------------
        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, "Ticket type name is required.", false, false, false);

        if (request.Price < 0)
            return (null, "Price must be 0 or greater.", false, false, false);

        if (request.Price != decimal.Truncate(request.Price))
            return (null, "Ticket price must be entered in whole LKR.", false, false, false);

        if (request.Capacity < 1)
            return (null, "Capacity must be at least 1.", false, false, false);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = request.Name.Trim(),
            Price = request.Price,
            Capacity = request.Capacity,
            CreatedAt = DateTime.UtcNow,
        };

        _context.TicketTypes.Add(ticketType);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "DB save failed while creating ticket type for Event {EventId}", eventId);
            return (null, "Failed to save ticket type. Please try again.", false, false, false);
        }

        _logger?.LogInformation(
            "Ticket type created. TicketTypeId={TicketTypeId} EventId={EventId} Name={Name} Price={Price} Capacity={Capacity}",
            ticketType.Id, eventId, ticketType.Name, ticketType.Price, ticketType.Capacity);

        return (MapToDto(ticketType), null, false, false, false);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<TicketTypeDto>? Result, bool IsNotFound, bool IsForbidden)> GetByEventIdAsync(
        Guid eventId,
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (eventItem == null)
            return (null, true, false);

        if (eventItem.OrganizerId != organizerId)
            return (null, false, true);

        var ticketTypes = await _context.TicketTypes
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return (ticketTypes.Select(MapToDto).ToList(), false, false);
    }

    /// <inheritdoc/>
    public async Task<(TicketTypeDto? Result, string? Error, bool IsNotFound, bool IsForbidden)> UpdateAsync(
        Guid eventId,
        Guid ticketTypeId,
        UpdateTicketTypeRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        // Load the ticket type together with its parent event so we can verify
        // ownership without a second round trip, and so the EventId relationship
        // is never touched during this update.
        var ticketType = await _context.TicketTypes
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == ticketTypeId && t.EventId == eventId, cancellationToken);

        if (ticketType == null || ticketType.Event == null)
        {
            return (null, "Ticket type not found.", true, false);
        }

        if (ticketType.Event.OrganizerId != organizerId)
        {
            _logger?.LogWarning(
                "UpdateTicketType forbidden: TicketType {TicketTypeId} belongs to Event owned by {ActualOrganizer}, request by {RequesterId}",
                ticketTypeId, ticketType.Event.OrganizerId, organizerId);
            return (null, "You do not have permission to update this ticket type.", false, true);
        }

        // ---- Domain Validation ------------------------------------------------
        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, "Ticket type name is required.", false, false);

        if (request.Price < 0)
            return (null, "Price must be 0 or greater.", false, false);

        if (request.Price != decimal.Truncate(request.Price))
            return (null, "Ticket price must be entered in whole LKR.", false, false);

        if (request.Capacity < 1)
            return (null, "Capacity must be at least 1.", false, false);

        // ---- Apply Update -------------------------------------------------
        // Note: Id, EventId, and CreatedAt are never modified here, preserving
        // the existing ticket type / event relationship (subtask 5).
        ticketType.Name = request.Name.Trim();
        ticketType.Price = request.Price;
        ticketType.Capacity = request.Capacity;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "DB save failed while updating TicketType {TicketTypeId}", ticketTypeId);
            return (null, "Failed to save ticket type changes. Please try again.", false, false);
        }

        _logger?.LogInformation(
            "Ticket type updated. TicketTypeId={TicketTypeId} EventId={EventId} Name={Name} Price={Price} Capacity={Capacity}",
            ticketType.Id, eventId, ticketType.Name, ticketType.Price, ticketType.Capacity);

        return (MapToDto(ticketType), null, false, false);
    }

    private static TicketTypeDto MapToDto(TicketType t) => new TicketTypeDto
    {
        Id = t.Id,
        EventId = t.EventId,
        Name = t.Name,
        Price = t.Price,
        Capacity = t.Capacity,
        BookedQuantity = t.BookedQuantity,
        CreatedAt = t.CreatedAt,
    };
}