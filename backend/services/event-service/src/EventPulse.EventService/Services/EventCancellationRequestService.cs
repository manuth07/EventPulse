using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;

namespace EventPulse.EventService.Services;

/// <summary>
/// Implements business logic for Event Cancellation Requests (EP-35 US-15).
/// Ensures that approved/published events are not directly cancelled by organizers,
/// but instead require submitting an auditable cancellation request for review.
/// </summary>
public class EventCancellationRequestService : IEventCancellationRequestService
{
    private readonly EventDbContext _context;
    private readonly ILogger<EventCancellationRequestService>? _logger;

    public EventCancellationRequestService(
        EventDbContext context,
        ILogger<EventCancellationRequestService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(EventCancellationRequestDto? Result, string? Error, bool IsNotFound, bool IsForbidden, bool IsInvalidState, bool IsConflict)> SubmitCancellationRequestAsync(
        Guid eventId,
        SubmitEventCancellationRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        // 1. Load Event
        var eventItem = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (eventItem == null)
            return (null, "Event not found.", true, false, false, false);

        // 2. Ownership Verification
        if (eventItem.OrganizerId != organizerId)
            return (null, "You do not have permission to cancel this event.", false, true, false, false);

        // 3. Event Lifecycle State Check — only Approved or Published events can receive a cancellation request
        if (eventItem.Status != EventStatus.Approved && eventItem.Status != EventStatus.Published)
        {
            return (null, "Cancellation requests can only be submitted for approved or published events.", false, false, true, false);
        }

        // 4. Duplicate / Concurrency Check — only ONE Pending cancellation request permitted per event at a time
        var hasPendingCancellation = await _context.EventCancellationRequests
            .AnyAsync(r => r.EventId == eventId && r.Status == EventCancellationRequestStatus.Pending, cancellationToken);

        if (hasPendingCancellation)
        {
            return (null, "This event already has a cancellation request pending review.", false, false, false, true);
        }

        // 5. Cross-Workflow Mutual Exclusion Check — cannot cancel if an update request is pending review
        var hasPendingUpdate = await _context.EventUpdateRequests
            .AnyAsync(r => r.EventId == eventId && r.Status == EventUpdateRequestStatus.Pending, cancellationToken);

        if (hasPendingUpdate)
        {
            return (null, "Cannot submit a cancellation request while an update request is pending review.", false, false, false, true);
        }

        // 6. Domain Field Validation
        if (string.IsNullOrWhiteSpace(request.Reason))
            return (null, "Cancellation reason is required.", false, false, false, false);

        var trimmedReason = request.Reason.Trim();
        if (trimmedReason.Length < 5 || trimmedReason.Length > 1000)
            return (null, "Cancellation reason must be between 5 and 1000 characters.", false, false, false, false);

        // 7. Create EventCancellationRequest Entity (Live Event record remains Approved/Published untouched!)
        var cancellationRequest = new EventCancellationRequest
        {
            Id = Guid.NewGuid(),
            EventId = eventItem.Id,
            OrganizerId = organizerId,
            Reason = trimmedReason,
            Status = EventCancellationRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        try
        {
            _context.EventCancellationRequests.Add(cancellationRequest);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger?.LogWarning(ex, "Conflict persisting EventCancellationRequest for Event {EventId}.", eventId);
            return (null, "This event already has a cancellation request pending review.", false, false, false, true);
        }

        _logger?.LogInformation(
            "EventCancellationRequest {RequestId} created for Event {EventId} by Organizer {OrganizerId}.",
            cancellationRequest.Id, eventItem.Id, organizerId);

        return (MapToDto(cancellationRequest), null, false, false, false, false);
    }

    /// <inheritdoc/>
    public async Task<(EventCancellationRequestDto? Result, string? Error, bool IsNotFound, bool IsForbidden)> GetCancellationRequestAsync(
        Guid eventId,
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify Event exists
        var eventItem = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (eventItem == null)
            return (null, "Event not found.", true, false);

        // 2. Ownership Verification
        if (eventItem.OrganizerId != organizerId)
            return (null, "You do not have permission to view cancellation requests for this event.", false, true);

        // 3. Load active or latest cancellation request
        var cancellationRequest = await _context.EventCancellationRequests
            .AsNoTracking()
            .Where(r => r.EventId == eventId)
            .OrderByDescending(r => r.Status == EventCancellationRequestStatus.Pending ? 1 : 0)
            .ThenByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (cancellationRequest == null)
            return (null, "No cancellation request found for this event.", true, false);

        return (MapToDto(cancellationRequest), null, false, false);
    }

    private static EventCancellationRequestDto MapToDto(EventCancellationRequest entity)
    {
        return new EventCancellationRequestDto
        {
            Id = entity.Id,
            EventId = entity.EventId,
            OrganizerId = entity.OrganizerId,
            Reason = entity.Reason,
            Status = entity.Status.ToString(),
            RequestedAt = entity.RequestedAt,
            ReviewedAt = entity.ReviewedAt,
            ReviewedBy = entity.ReviewedBy,
            ReviewComment = entity.ReviewComment
        };
    }
}
