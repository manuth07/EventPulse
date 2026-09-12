using EventPulse.EventService.DTOs;

namespace EventPulse.EventService.Services;

public interface ITicketTypeService
{
    /// <summary>
    /// Creates a ticket type for an event owned by the given organizer.
    /// Eligibility: event must exist, be owned by organizerId, and be Approved or Published.
    /// </summary>
    Task<(TicketTypeDto? Result, string? Error, bool IsNotFound, bool IsForbidden, bool IsInvalidState)> CreateAsync(
        Guid eventId,
        CreateTicketTypeRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all ticket types configured for an event owned by the given organizer.
    /// </summary>
    Task<(IReadOnlyList<TicketTypeDto>? Result, bool IsNotFound, bool IsForbidden)> GetByEventIdAsync(
        Guid eventId,
        Guid organizerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing ticket type's name, price, and capacity.
    /// Only permitted for ticket types belonging to events owned by the requesting organizer.
    /// EventId association is preserved and cannot be changed via this operation.
    /// </summary>
    Task<(TicketTypeDto? Result, string? Error, bool IsNotFound, bool IsForbidden)> UpdateAsync(
        Guid eventId,
        Guid ticketTypeId,
        UpdateTicketTypeRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default);
}