using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Storage;

namespace EventPulse.EventService.Services;

/// <summary>
/// Implements business logic for Event Cancellation Requests (EP-35 US-15).
/// Ensures that approved/published events are not directly cancelled by organizers,
/// but instead require submitting an auditable cancellation request for review.
/// </summary>
public class EventCancellationRequestService : IEventCancellationRequestService
{
    private readonly EventDbContext _context;
    private readonly IEventImageStorage? _imageStorage;
    private readonly ILogger<EventCancellationRequestService>? _logger;

    public EventCancellationRequestService(
        EventDbContext context,
        IEventImageStorage? imageStorage = null,
        ILogger<EventCancellationRequestService>? logger = null)
    {
        _context = context;
        _imageStorage = imageStorage;
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AdminEventCancellationReviewDto>> GetPendingCancellationRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        var requests = await _context.EventCancellationRequests
            .AsNoTracking()
            .Include(r => r.Event)
                .ThenInclude(e => e.TicketTypes)
            .Where(r => r.Status == EventCancellationRequestStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        return requests.Select(MapToAdminReviewDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<AdminEventCancellationReviewDto?> GetCancellationRequestReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.EventCancellationRequests
            .AsNoTracking()
            .Include(r => r.Event)
                .ThenInclude(e => e.TicketTypes)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null)
            return null;

        return MapToAdminReviewDto(request);
    }

    /// <inheritdoc/>
    public async Task<(AdminEventCancellationReviewDto? Result, string? Error, bool IsNotFound, bool IsInvalidState)> ApproveCancellationRequestAsync(
        Guid requestId,
        Guid reviewerId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var cancellationRequest = await _context.EventCancellationRequests
            .Include(r => r.Event)
                .ThenInclude(e => e.TicketTypes)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (cancellationRequest == null)
        {
            return (null, "Cancellation request not found.", true, false);
        }

        // Concurrency / Duplicate review guard
        if (cancellationRequest.Status != EventCancellationRequestStatus.Pending)
        {
            _logger?.LogWarning(
                "ApproveCancellationRequest rejected: Request {RequestId} has status {Status}, expected Pending.",
                requestId, cancellationRequest.Status);
            return (null, $"Only Pending cancellation requests can be approved. Current status: {cancellationRequest.Status}.", false, true);
        }

        var liveEvent = cancellationRequest.Event;
        if (liveEvent == null)
        {
            return (null, "Associated event not found.", true, false);
        }

        if (liveEvent.Status == EventStatus.Cancelled)
        {
            return (null, "Event is already cancelled.", false, true);
        }

        if (liveEvent.Status != EventStatus.Approved && liveEvent.Status != EventStatus.Published)
        {
            return (null, $"Cannot cancel event in status: {liveEvent.Status}.", false, true);
        }

        // 1. Atomically transition live Event to Cancelled
        liveEvent.Status = EventStatus.Cancelled;

        // 2. Mark request as Approved and record reviewer metadata
        cancellationRequest.Status = EventCancellationRequestStatus.Approved;
        cancellationRequest.ReviewedAt = DateTime.UtcNow;
        cancellationRequest.ReviewedBy = reviewerId;

        if (!string.IsNullOrWhiteSpace(notes))
            cancellationRequest.ReviewComment = notes.Trim();

        // 3. Save atomically in single transaction
        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "EventCancellationRequest {RequestId} approved by Admin {ReviewerId}. Live Event {EventId} transitioned to Cancelled.",
            requestId, reviewerId, liveEvent.Id);

        return (MapToAdminReviewDto(cancellationRequest), null, false, false);
    }

    /// <inheritdoc/>
    public async Task<(AdminEventCancellationReviewDto? Result, string? Error, bool IsNotFound, bool IsInvalidState)> RejectCancellationRequestAsync(
        Guid requestId,
        Guid reviewerId,
        string notes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return (null, "Rejection feedback is required.", false, true);
        }

        var cancellationRequest = await _context.EventCancellationRequests
            .Include(r => r.Event)
                .ThenInclude(e => e.TicketTypes)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (cancellationRequest == null)
        {
            return (null, "Cancellation request not found.", true, false);
        }

        // Concurrency / Duplicate review guard
        if (cancellationRequest.Status != EventCancellationRequestStatus.Pending)
        {
            _logger?.LogWarning(
                "RejectCancellationRequest rejected: Request {RequestId} has status {Status}, expected Pending.",
                requestId, cancellationRequest.Status);
            return (null, $"Only Pending cancellation requests can be rejected. Current status: {cancellationRequest.Status}.", false, true);
        }

        var liveEvent = cancellationRequest.Event;

        // Live Event status remains completely unchanged (Approved or Published)
        cancellationRequest.Status = EventCancellationRequestStatus.Rejected;
        cancellationRequest.ReviewedAt = DateTime.UtcNow;
        cancellationRequest.ReviewedBy = reviewerId;
        cancellationRequest.ReviewComment = notes.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "EventCancellationRequest {RequestId} rejected by Admin {ReviewerId}. Live Event {EventId} remains in status {EventStatus}.",
            requestId, reviewerId, cancellationRequest.EventId, liveEvent?.Status);

        return (MapToAdminReviewDto(cancellationRequest), null, false, false);
    }

    private AdminEventCancellationReviewDto MapToAdminReviewDto(EventCancellationRequest entity)
    {
        var liveEvent = entity.Event;
        var ticketsSold = liveEvent?.TicketTypes?.Sum(t => t.BookedQuantity) ?? 0;
        var capacity = liveEvent?.TicketTypes?.Sum(t => t.Capacity) ?? 0;

        return new AdminEventCancellationReviewDto
        {
            Id = entity.Id,
            EventId = entity.EventId,
            OrganizerId = entity.OrganizerId,
            Reason = entity.Reason,
            Status = entity.Status.ToString(),
            RequestedAt = entity.RequestedAt,
            ReviewedAt = entity.ReviewedAt,
            ReviewedBy = entity.ReviewedBy,
            ReviewComment = entity.ReviewComment,

            EventTitle = liveEvent?.Title ?? string.Empty,
            EventDescription = liveEvent?.Description ?? string.Empty,
            EventVenue = liveEvent?.Venue ?? string.Empty,
            EventDate = liveEvent?.EventDate ?? default,
            EventPrice = liveEvent?.Price ?? 0m,
            EventStatus = liveEvent?.Status.ToString() ?? string.Empty,
            Category = liveEvent?.Category,
            VenueType = liveEvent?.VenueType,
            ImageUrl = _imageStorage?.GetPublicUrl(liveEvent?.ImageBlobName),
            CoverUrl = _imageStorage?.GetPublicUrl(liveEvent?.CoverBlobName),

            TotalTicketsSold = ticketsSold,
            TotalCapacity = capacity
        };
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
