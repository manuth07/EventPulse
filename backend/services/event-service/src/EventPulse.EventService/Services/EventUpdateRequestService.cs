using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Storage;

namespace EventPulse.EventService.Services;

/// <summary>
/// Implements business logic for Event Update Requests (EP-34 US-14).
/// Ensures that approved/published events are not directly updated by organizers,
/// but instead routed through a formal, auditable review workflow.
/// </summary>
public class EventUpdateRequestService : IEventUpdateRequestService
{
    private readonly EventDbContext _context;
    private readonly IEventImageStorage _imageStorage;
    private readonly ILogger<EventUpdateRequestService>? _logger;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public EventUpdateRequestService(
        EventDbContext context,
        IEventImageStorage imageStorage,
        ILogger<EventUpdateRequestService>? logger = null)
    {
        _context = context;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(EventUpdateRequestDto? Result, string? Error, bool IsNotFound, bool IsForbidden, bool IsInvalidState, bool IsConflict)> SubmitUpdateRequestAsync(
        Guid eventId,
        SubmitEventUpdateRequest request,
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
            return (null, "You do not have permission to modify this event.", false, true, false, false);

        // 3. Event Lifecycle State Check — only Approved or Published events can receive an update request
        if (eventItem.Status != EventStatus.Approved && eventItem.Status != EventStatus.Published)
        {
            return (null, "Update requests can only be submitted for approved or published events.", false, false, true, false);
        }

        // 4. Duplicate / Concurrency Check — only ONE Pending request permitted per event at a time
        var hasPendingRequest = await _context.EventUpdateRequests
            .AnyAsync(r => r.EventId == eventId && r.Status == EventUpdateRequestStatus.Pending, cancellationToken);

        if (hasPendingRequest)
        {
            return (null, "This event already has an update request pending review.", false, false, false, true);
        }

        // 5. Domain Field Validation (aligned with EventSubmissionService)
        if (string.IsNullOrWhiteSpace(request.Title))
            return (null, "Title is required.", false, false, false, false);

        if (request.Title.Trim().Length < 3 || request.Title.Trim().Length > 200)
            return (null, "Title must be between 3 and 200 characters.", false, false, false, false);

        if (string.IsNullOrWhiteSpace(request.Description))
            return (null, "Description is required.", false, false, false, false);

        if (request.Description.Trim().Length < 10 || request.Description.Trim().Length > 2000)
            return (null, "Description must be between 10 and 2000 characters.", false, false, false, false);

        if (string.IsNullOrWhiteSpace(request.Venue))
            return (null, "Venue is required.", false, false, false, false);

        if (request.Venue.Trim().Length < 3 || request.Venue.Trim().Length > 200)
            return (null, "Venue must be between 3 and 200 characters.", false, false, false, false);

        var eventDateUtc = request.EventDate.Kind == DateTimeKind.Utc
            ? request.EventDate
            : (request.EventDate.Kind == DateTimeKind.Local
                ? request.EventDate.ToUniversalTime()
                : DateTime.SpecifyKind(request.EventDate, DateTimeKind.Utc));

        if (eventDateUtc <= DateTime.UtcNow)
            return (null, "EventDate must be in the future.", false, false, false, false);

        string? canonicalCategory = null;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (!EventSubmissionService.AllowedCategories.Contains(request.Category.Trim()))
                return (null, $"Unsupported event category '{request.Category}'.", false, false, false, false);

            canonicalCategory = EventSubmissionService.AllowedCategories.First(c =>
                string.Equals(c, request.Category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        string? canonicalVenueType = null;
        if (!string.IsNullOrWhiteSpace(request.VenueType))
        {
            if (!EventSubmissionService.AllowedVenueTypes.Contains(request.VenueType.Trim()))
                return (null, $"Unsupported venue type '{request.VenueType}'. Supported values: Indoor, Outdoor.", false, false, false, false);

            canonicalVenueType = EventSubmissionService.AllowedVenueTypes.First(v =>
                string.Equals(v, request.VenueType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // 6. Safe Image Replacement Handling
        // New images are uploaded and referenced by the update request.
        // The approved event's existing images are NEVER modified or deleted.
        string? newImageBlobName = null;
        string? newCoverBlobName = null;

        if (request.Image is not null && request.Image.Length > 0)
        {
            var contentType = request.Image.ContentType?.Trim() ?? string.Empty;
            if (!AllowedContentTypes.Contains(contentType))
                return (null, $"Unsupported image type '{contentType}'. Accepted: JPEG, PNG, WebP.", false, false, false, false);

            if (request.Image.Length > MaxFileSizeBytes)
                return (null, $"Image exceeds the 5 MB maximum (received {request.Image.Length:N0} bytes).", false, false, false, false);

            try
            {
                await using var stream = request.Image.OpenReadStream();
                newImageBlobName = await _imageStorage.UploadAsync(
                    stream,
                    contentType,
                    request.Image.FileName,
                    "event-posters",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to upload proposed poster image for update request.");
                return (null, "Failed to upload replacement poster. Please try again.", false, false, false, false);
            }
        }

        if (request.CoverImage is not null && request.CoverImage.Length > 0)
        {
            var contentType = request.CoverImage.ContentType?.Trim() ?? string.Empty;
            if (!AllowedContentTypes.Contains(contentType))
            {
                if (newImageBlobName != null)
                    await _imageStorage.DeleteAsync(newImageBlobName, cancellationToken);

                return (null, $"Unsupported cover image type '{contentType}'. Accepted: JPEG, PNG, WebP.", false, false, false, false);
            }

            if (request.CoverImage.Length > MaxFileSizeBytes)
            {
                if (newImageBlobName != null)
                    await _imageStorage.DeleteAsync(newImageBlobName, cancellationToken);

                return (null, $"Cover image exceeds the 5 MB maximum (received {request.CoverImage.Length:N0} bytes).", false, false, false, false);
            }

            try
            {
                await using var stream = request.CoverImage.OpenReadStream();
                newCoverBlobName = await _imageStorage.UploadAsync(
                    stream,
                    contentType,
                    request.CoverImage.FileName,
                    "event-covers",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to upload proposed cover image for update request.");
                if (newImageBlobName != null)
                    await _imageStorage.DeleteAsync(newImageBlobName, cancellationToken);

                return (null, "Failed to upload replacement cover banner. Please try again.", false, false, false, false);
            }
        }

        // 7. Create EventUpdateRequest Entity (Live Event remains completely untouched!)
        var updateRequest = new EventUpdateRequest
        {
            Id = Guid.NewGuid(),
            EventId = eventItem.Id,
            OrganizerId = organizerId,
            Status = EventUpdateRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Venue = request.Venue.Trim(),
            EventDate = eventDateUtc,
            Category = canonicalCategory ?? eventItem.Category,
            VenueType = canonicalVenueType ?? eventItem.VenueType,
            ImageBlobName = newImageBlobName ?? eventItem.ImageBlobName,
            CoverBlobName = newCoverBlobName ?? eventItem.CoverBlobName
        };

        try
        {
            _context.EventUpdateRequests.Add(updateRequest);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger?.LogWarning(ex, "Conflict persisting EventUpdateRequest for Event {EventId}.", eventId);

            // Clean up newly uploaded blobs on persistence error
            if (newImageBlobName != null)
                await _imageStorage.DeleteAsync(newImageBlobName, cancellationToken);
            if (newCoverBlobName != null)
                await _imageStorage.DeleteAsync(newCoverBlobName, cancellationToken);

            // If unique constraint violated due to race condition
            return (null, "This event already has an update request pending review.", false, false, false, true);
        }

        _logger?.LogInformation(
            "EventUpdateRequest {RequestId} created for Event {EventId} by Organizer {OrganizerId}.",
            updateRequest.Id, eventItem.Id, organizerId);

        return (MapToDto(eventItem, updateRequest), null, false, false, false, false);
    }

    /// <inheritdoc/>
    public async Task<(EventUpdateRequestDto? Result, string? Error, bool IsNotFound, bool IsForbidden)> GetUpdateRequestAsync(
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
            return (null, "You do not have permission to view update requests for this event.", false, true);

        // 3. Load active or latest update request
        var updateRequest = await _context.EventUpdateRequests
            .AsNoTracking()
            .Where(r => r.EventId == eventId)
            .OrderByDescending(r => r.Status == EventUpdateRequestStatus.Pending ? 1 : 0)
            .ThenByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (updateRequest == null)
            return (null, "No update request found for this event.", true, false);

        return (MapToDto(eventItem, updateRequest), null, false, false);
    }

    // =========================================================================
    // EP-210 / US-14: ADMINISTRATOR REVIEW METHODS
    // =========================================================================

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AdminEventUpdateComparisonDto>> GetPendingUpdateRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        var requests = await _context.EventUpdateRequests
            .Include(r => r.Event)
            .AsNoTracking()
            .Where(r => r.Status == EventUpdateRequestStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        return requests.Select(r => MapToComparisonDto(r.Event, r)).ToList();
    }

    /// <inheritdoc/>
    public async Task<AdminEventUpdateComparisonDto?> GetUpdateRequestReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.EventUpdateRequests
            .Include(r => r.Event)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null || request.Event == null)
            return null;

        return MapToComparisonDto(request.Event, request);
    }

    /// <inheritdoc/>
    public async Task<(AdminEventUpdateComparisonDto? Result, string? Error, bool IsNotFound, bool IsInvalidState)> ApproveUpdateRequestAsync(
        Guid requestId,
        Guid reviewerId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var updateRequest = await _context.EventUpdateRequests
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (updateRequest == null)
        {
            return (null, "Update request not found.", true, false);
        }

        // Concurrency / Duplicate review guard
        if (updateRequest.Status != EventUpdateRequestStatus.Pending)
        {
            _logger?.LogWarning(
                "ApproveUpdateRequest rejected: Request {RequestId} has status {Status}, expected Pending.",
                requestId, updateRequest.Status);
            return (null, $"Only Pending update requests can be approved. Current status: {updateRequest.Status}.", false, true);
        }

        var liveEvent = updateRequest.Event;
        if (liveEvent == null)
        {
            return (null, "Associated event not found.", true, false);
        }

        if (liveEvent.Status != EventStatus.Approved && liveEvent.Status != EventStatus.Published)
        {
            return (null, $"Cannot apply update to event in status: {liveEvent.Status}.", false, true);
        }

        // 1. Atomically apply proposed fields to live Event
        liveEvent.Title = updateRequest.Title;
        liveEvent.Description = updateRequest.Description;
        liveEvent.Venue = updateRequest.Venue;
        liveEvent.EventDate = updateRequest.EventDate;

        if (!string.IsNullOrWhiteSpace(updateRequest.Category))
            liveEvent.Category = updateRequest.Category;

        if (!string.IsNullOrWhiteSpace(updateRequest.VenueType))
            liveEvent.VenueType = updateRequest.VenueType;

        if (!string.IsNullOrWhiteSpace(updateRequest.ImageBlobName))
            liveEvent.ImageBlobName = updateRequest.ImageBlobName;

        if (!string.IsNullOrWhiteSpace(updateRequest.CoverBlobName))
            liveEvent.CoverBlobName = updateRequest.CoverBlobName;

        // 2. Mark update request Approved and store review metadata
        updateRequest.Status = EventUpdateRequestStatus.Approved;
        updateRequest.ReviewedAt = DateTime.UtcNow;
        updateRequest.ReviewedBy = reviewerId;

        if (!string.IsNullOrWhiteSpace(notes))
            updateRequest.ReviewComment = notes.Trim();

        // 3. Save atomically in single transaction
        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "EventUpdateRequest {RequestId} approved by Admin {ReviewerId}. Live Event {EventId} updated.",
            requestId, reviewerId, liveEvent.Id);

        return (MapToComparisonDto(liveEvent, updateRequest), null, false, false);
    }

    /// <inheritdoc/>
    public async Task<(AdminEventUpdateComparisonDto? Result, string? Error, bool IsNotFound, bool IsInvalidState)> RejectUpdateRequestAsync(
        Guid requestId,
        Guid reviewerId,
        string notes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return (null, "Rejection feedback is required.", false, true);
        }

        var updateRequest = await _context.EventUpdateRequests
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (updateRequest == null)
        {
            return (null, "Update request not found.", true, false);
        }

        // Concurrency / Duplicate review guard
        if (updateRequest.Status != EventUpdateRequestStatus.Pending)
        {
            _logger?.LogWarning(
                "RejectUpdateRequest rejected: Request {RequestId} has status {Status}, expected Pending.",
                requestId, updateRequest.Status);
            return (null, $"Only Pending update requests can be rejected. Current status: {updateRequest.Status}.", false, true);
        }

        var liveEvent = updateRequest.Event;

        // Live Event remains completely untouched!
        updateRequest.Status = EventUpdateRequestStatus.Rejected;
        updateRequest.ReviewedAt = DateTime.UtcNow;
        updateRequest.ReviewedBy = reviewerId;
        updateRequest.ReviewComment = notes.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "EventUpdateRequest {RequestId} rejected by Admin {ReviewerId}. Live Event {EventId} left unchanged.",
            requestId, reviewerId, updateRequest.EventId);

        return (MapToComparisonDto(liveEvent ?? new Event(), updateRequest), null, false, false);
    }

    // =========================================================================
    // HELPER: Change Detection & DTO Mapping
    // =========================================================================
    private EventUpdateRequestDto MapToDto(Event originalEvent, EventUpdateRequest request)
    {
        var hasTitleChanged = !string.Equals(originalEvent.Title, request.Title, StringComparison.Ordinal);
        var hasDescriptionChanged = !string.Equals(originalEvent.Description, request.Description, StringComparison.Ordinal);
        var hasVenueChanged = !string.Equals(originalEvent.Venue, request.Venue, StringComparison.OrdinalIgnoreCase);
        var hasDateChanged = originalEvent.EventDate != request.EventDate;
        var hasCategoryChanged = !string.Equals(originalEvent.Category, request.Category, StringComparison.OrdinalIgnoreCase);
        var hasVenueTypeChanged = !string.Equals(originalEvent.VenueType, request.VenueType, StringComparison.OrdinalIgnoreCase);
        var hasImageChanged = !string.Equals(originalEvent.ImageBlobName, request.ImageBlobName, StringComparison.Ordinal);
        var hasCoverChanged = !string.Equals(originalEvent.CoverBlobName, request.CoverBlobName, StringComparison.Ordinal);

        // Major changes: Location (Venue) or Date changes
        var isMajorChange = hasVenueChanged || hasDateChanged;

        return new EventUpdateRequestDto
        {
            Id = request.Id,
            EventId = request.EventId,
            OrganizerId = request.OrganizerId,
            Status = request.Status.ToString(),
            RequestedAt = request.RequestedAt,
            ReviewedAt = request.ReviewedAt,
            ReviewedBy = request.ReviewedBy,
            ReviewComment = request.ReviewComment,
            Title = request.Title,
            Description = request.Description,
            Venue = request.Venue,
            EventDate = request.EventDate,
            Category = request.Category,
            VenueType = request.VenueType,
            ImageBlobName = request.ImageBlobName,
            ImageUrl = _imageStorage.GetPublicUrl(request.ImageBlobName),
            CoverBlobName = request.CoverBlobName,
            CoverUrl = _imageStorage.GetPublicUrl(request.CoverBlobName),
            HasTitleChanged = hasTitleChanged,
            HasDescriptionChanged = hasDescriptionChanged,
            HasVenueChanged = hasVenueChanged,
            HasDateChanged = hasDateChanged,
            HasCategoryChanged = hasCategoryChanged,
            HasVenueTypeChanged = hasVenueTypeChanged,
            HasImageChanged = hasImageChanged,
            HasCoverChanged = hasCoverChanged,
            IsMajorChange = isMajorChange
        };
    }

    private AdminEventUpdateComparisonDto MapToComparisonDto(Event currentEvent, EventUpdateRequest request)
    {
        var hasTitleChanged = !string.Equals(currentEvent.Title, request.Title, StringComparison.Ordinal);
        var hasDescriptionChanged = !string.Equals(currentEvent.Description, request.Description, StringComparison.Ordinal);
        var hasVenueChanged = !string.Equals(currentEvent.Venue, request.Venue, StringComparison.OrdinalIgnoreCase);
        var hasDateChanged = currentEvent.EventDate != request.EventDate;
        var hasCategoryChanged = !string.Equals(currentEvent.Category, request.Category, StringComparison.OrdinalIgnoreCase);
        var hasVenueTypeChanged = !string.Equals(currentEvent.VenueType, request.VenueType, StringComparison.OrdinalIgnoreCase);
        var hasImageChanged = !string.Equals(currentEvent.ImageBlobName, request.ImageBlobName, StringComparison.Ordinal);
        var hasCoverChanged = !string.Equals(currentEvent.CoverBlobName, request.CoverBlobName, StringComparison.Ordinal);

        return new AdminEventUpdateComparisonDto
        {
            Id = request.Id,
            EventId = request.EventId,
            OrganizerId = request.OrganizerId,
            Status = request.Status.ToString(),
            RequestedAt = request.RequestedAt,
            ReviewedAt = request.ReviewedAt,
            ReviewedBy = request.ReviewedBy,
            ReviewComment = request.ReviewComment,
            Current = new EventValuesDto
            {
                Title = currentEvent.Title,
                Description = currentEvent.Description,
                Venue = currentEvent.Venue,
                EventDate = currentEvent.EventDate,
                Category = currentEvent.Category,
                VenueType = currentEvent.VenueType,
                ImageBlobName = currentEvent.ImageBlobName,
                ImageUrl = _imageStorage.GetPublicUrl(currentEvent.ImageBlobName),
                CoverBlobName = currentEvent.CoverBlobName,
                CoverUrl = _imageStorage.GetPublicUrl(currentEvent.CoverBlobName)
            },
            Proposed = new EventValuesDto
            {
                Title = request.Title,
                Description = request.Description,
                Venue = request.Venue,
                EventDate = request.EventDate,
                Category = request.Category,
                VenueType = request.VenueType,
                ImageBlobName = request.ImageBlobName,
                ImageUrl = _imageStorage.GetPublicUrl(request.ImageBlobName),
                CoverBlobName = request.CoverBlobName,
                CoverUrl = _imageStorage.GetPublicUrl(request.CoverBlobName)
            },
            HasTitleChanged = hasTitleChanged,
            HasDescriptionChanged = hasDescriptionChanged,
            HasVenueChanged = hasVenueChanged,
            HasDateChanged = hasDateChanged,
            HasCategoryChanged = hasCategoryChanged,
            HasVenueTypeChanged = hasVenueTypeChanged,
            HasImageChanged = hasImageChanged,
            HasCoverChanged = hasCoverChanged
        };
    }
}
