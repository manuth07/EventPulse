using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Storage;

namespace EventPulse.EventService.Services;

/// <summary>
/// Implements Administrator event review business rules and state transitions.
/// Permitted state machine transitions in EP-31:
///   Pending -> Approved
///   Pending -> Rejected
/// Any other transition is rejected with a conflict error.
/// </summary>
public class EventReviewService : IEventReviewService
{
    private readonly EventDbContext _context;
    private readonly IEventImageStorage _imageStorage;
    private readonly ILogger<EventReviewService>? _logger;

    public EventReviewService(
        EventDbContext context,
        IEventImageStorage imageStorage,
        ILogger<EventReviewService>? logger = null)
    {
        _context = context;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AdminEventReviewDto>> GetPendingEventsAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return events.Select(MapToReviewDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<AdminEventReviewDto?> GetPendingEventByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.Status == EventStatus.Pending, cancellationToken);

        if (eventItem == null)
            return null;

        return MapToReviewDto(eventItem);
    }

    /// <inheritdoc/>
    public async Task<(AdminEventReviewDto? Result, string? Error, bool IsNotFound)> ApproveEventAsync(
        Guid id,
        Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (eventItem == null)
        {
            return (null, "Event not found.", true);
        }

        if (eventItem.Status != EventStatus.Pending)
        {
            _logger?.LogWarning(
                "ApproveEvent rejected: Event {EventId} has status {Status}, expected Pending.",
                id, eventItem.Status);
            return (null, $"Only Pending events can be approved. Current status: {eventItem.Status}.", false);
        }

        eventItem.Status = EventStatus.Approved;
        eventItem.ReviewedAt = DateTime.UtcNow;
        eventItem.ReviewedBy = reviewerId;

        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "Event approved. EventId={EventId}, ReviewedBy={ReviewerId}, ReviewedAt={ReviewedAt}",
            eventItem.Id, reviewerId, eventItem.ReviewedAt);

        return (MapToReviewDto(eventItem), null, false);
    }

    /// <inheritdoc/>
    public async Task<(AdminEventReviewDto? Result, string? Error, bool IsNotFound)> RejectEventAsync(
        Guid id,
        Guid reviewerId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return (null, "Rejection feedback is required.", false);
        }

        var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (eventItem == null)
        {
            return (null, "Event not found.", true);
        }

        if (eventItem.Status != EventStatus.Pending)
        {
            _logger?.LogWarning(
                "RejectEvent rejected: Event {EventId} has status {Status}, expected Pending.",
                id, eventItem.Status);
            return (null, $"Only Pending events can be rejected. Current status: {eventItem.Status}.", false);
        }

        eventItem.Status = EventStatus.Rejected;
        eventItem.ReviewedAt = DateTime.UtcNow;
        eventItem.ReviewedBy = reviewerId;
        eventItem.ReviewComment = notes.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "Event rejected. EventId={EventId}, ReviewedBy={ReviewerId}, ReviewedAt={ReviewedAt}, Notes={Notes}",
            eventItem.Id, reviewerId, eventItem.ReviewedAt, notes);

        return (MapToReviewDto(eventItem), null, false);
    }

    private AdminEventReviewDto MapToReviewDto(Event e)
    {
        return new AdminEventReviewDto
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            Venue = e.Venue,
            EventDate = e.EventDate,
            Price = e.Price,
            Status = e.Status.ToString(),
            CreatedAt = e.CreatedAt,
            ImageUrl = _imageStorage.GetPublicUrl(e.ImageBlobName),
            OrganizerId = e.OrganizerId,
            ReviewedAt = e.ReviewedAt,
            ReviewedBy = e.ReviewedBy,
            ReviewComment = e.ReviewComment,
        };
    }
}
