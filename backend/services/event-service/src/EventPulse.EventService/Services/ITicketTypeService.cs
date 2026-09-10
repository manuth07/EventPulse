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
}