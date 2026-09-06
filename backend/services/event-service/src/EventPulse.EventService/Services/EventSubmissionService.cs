using Microsoft.EntityFrameworkCore;
using EventPulse.EventService.Data;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using EventPulse.EventService.Storage;

namespace EventPulse.EventService.Services;

/// <summary>
/// Implements event submission business rules including poster upload.
/// Flow:
///   1. Validate event fields
///   2. Validate poster (type, size)
///   3. Upload poster → receive ImageBlobName
///   4. Persist Event entity (Status=Pending, OrganizerId from JWT, ImageBlobName)
///   5. On DB failure: best-effort delete the uploaded blob (compensating cleanup)
/// </summary>
public class EventSubmissionService : IEventSubmissionService
{
    private readonly EventDbContext _context;
    private readonly IEventImageStorage _imageStorage;
    private readonly ILogger<EventSubmissionService>? _logger;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public EventSubmissionService(
        EventDbContext context,
        IEventImageStorage imageStorage,
        ILogger<EventSubmissionService>? logger = null)
    {
        _context = context;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(EventSubmissionResponseDto? Result, string? Error)> CreateAsync(
        CreateEventRequest request,
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        // ---- Domain Validation (fields) ---------------------------------------
        if (string.IsNullOrWhiteSpace(request.Title))
            return (null, "Title is required.");

        if (string.IsNullOrWhiteSpace(request.Description))
            return (null, "Description is required.");

        if (string.IsNullOrWhiteSpace(request.Venue))
            return (null, "Venue is required.");

        if (request.EventDate <= DateTime.UtcNow)
            return (null, "EventDate must be in the future.");

        if (request.Price < 0)
            return (null, "Price must be 0 or greater.");

        // ---- Poster Validation -----------------------------------------------
        if (request.Image is null || request.Image.Length == 0)
            return (null, "Event poster image is required.");

        var contentType = request.Image.ContentType?.Trim() ?? string.Empty;
        if (!AllowedContentTypes.Contains(contentType))
            return (null, $"Unsupported image type '{contentType}'. Accepted: JPEG, PNG, WebP.");

        if (request.Image.Length > MaxFileSizeBytes)
            return (null, $"Image exceeds the 5 MB maximum (received {request.Image.Length:N0} bytes).");

        // ---- Upload Poster ---------------------------------------------------
        string imageBlobName;
        try
        {
            await using var stream = request.Image.OpenReadStream();
            imageBlobName = await _imageStorage.UploadAsync(
                stream,
                contentType,
                request.Image.FileName,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning("Poster upload validation failed: {Message}", ex.Message);
            return (null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during poster upload.");
            return (null, "Failed to upload event poster. Please try again.");
        }

        // ---- Persist Event ---------------------------------------------------
        var newEvent = new Event
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Venue = request.Venue.Trim(),
            EventDate = request.EventDate,
            Price = request.Price,
            // Server-controlled — never from frontend
            Status = EventStatus.Pending,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            ImageBlobName = imageBlobName,
        };

        _context.Events.Add(newEvent);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "DB save failed after poster upload. Attempting compensating blob deletion. BlobName={BlobName}",
                imageBlobName);

            // Compensating cleanup: remove the orphaned blob
            await _imageStorage.DeleteAsync(imageBlobName, cancellationToken);

            return (null, "Failed to save event. Please try again.");
        }

        _logger?.LogInformation(
            "Event submitted. EventId={EventId} OrganizerId={OrganizerId} Status={Status} ImageBlobName={BlobName}",
            newEvent.Id, organizerId, newEvent.Status, imageBlobName);

        var response = new EventSubmissionResponseDto
        {
            Id = newEvent.Id,
            Title = newEvent.Title,
            Description = newEvent.Description,
            Venue = newEvent.Venue,
            EventDate = newEvent.EventDate,
            Price = newEvent.Price,
            Status = newEvent.Status.ToString(),
            OrganizerId = newEvent.OrganizerId,
            CreatedAt = newEvent.CreatedAt,
            ImageUrl = _imageStorage.GetPublicUrl(imageBlobName),
        };

        return (response, null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrganizerEventSubmissionDto>> GetOrganizerSubmissionsAsync(
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == organizerId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return events.Select(e => new OrganizerEventSubmissionDto
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
            ReviewedAt = e.ReviewedAt,
            ReviewComment = e.ReviewComment,
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<OrganizerEventSubmissionDto?> GetOrganizerSubmissionByIdAsync(
        Guid eventId,
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (eventItem == null || eventItem.OrganizerId != organizerId)
            return null;

        return new OrganizerEventSubmissionDto
        {
            Id = eventItem.Id,
            Title = eventItem.Title,
            Description = eventItem.Description,
            Venue = eventItem.Venue,
            EventDate = eventItem.EventDate,
            Price = eventItem.Price,
            Status = eventItem.Status.ToString(),
            CreatedAt = eventItem.CreatedAt,
            ImageUrl = _imageStorage.GetPublicUrl(eventItem.ImageBlobName),
            ReviewedAt = eventItem.ReviewedAt,
            ReviewComment = eventItem.ReviewComment,
        };
    }

    /// <inheritdoc/>
    public async Task<(OrganizerEventSubmissionDto? Result, string? Error, bool IsNotFound, bool IsForbidden, bool IsInvalidState)> ResubmitAsync(
        Guid eventId,
        ResubmitEventRequest request,
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
                "ResubmitAsync forbidden: Event {EventId} belongs to Organizer {ActualOrganizer}, request by {RequesterId}",
                eventId, eventItem.OrganizerId, organizerId);
            return (null, "You do not have permission to resubmit this event.", false, true, false);
        }

        if (eventItem.Status != EventStatus.Rejected)
        {
            _logger?.LogWarning(
                "ResubmitAsync rejected: Event {EventId} has status {Status}, expected Rejected.",
                eventId, eventItem.Status);
            return (null, $"Only Rejected events can be edited and resubmitted. Current status: {eventItem.Status}.", false, false, true);
        }

        // Domain validation
        if (string.IsNullOrWhiteSpace(request.Title))
            return (null, "Title is required.", false, false, false);

        if (string.IsNullOrWhiteSpace(request.Description))
            return (null, "Description is required.", false, false, false);

        if (string.IsNullOrWhiteSpace(request.Venue))
            return (null, "Venue is required.", false, false, false);

        var eventDateUtc = request.EventDate.Kind == DateTimeKind.Utc
            ? request.EventDate
            : (request.EventDate.Kind == DateTimeKind.Local
                ? request.EventDate.ToUniversalTime()
                : DateTime.SpecifyKind(request.EventDate, DateTimeKind.Utc));

        if (eventDateUtc <= DateTime.UtcNow)
            return (null, "EventDate must be in the future.", false, false, false);

        if (request.Price < 0)
            return (null, "Price must be 0 or greater.", false, false, false);

        string? oldImageBlobName = null;
        string? newImageBlobName = null;

        // Poster replacement if new image is provided
        if (request.Image is not null && request.Image.Length > 0)
        {
            var contentType = request.Image.ContentType?.Trim() ?? string.Empty;
            if (!AllowedContentTypes.Contains(contentType))
                return (null, $"Unsupported image type '{contentType}'. Accepted: JPEG, PNG, WebP.", false, false, false);

            if (request.Image.Length > MaxFileSizeBytes)
                return (null, $"Image exceeds the 5 MB maximum (received {request.Image.Length:N0} bytes).", false, false, false);

            try
            {
                await using var stream = request.Image.OpenReadStream();
                newImageBlobName = await _imageStorage.UploadAsync(
                    stream,
                    contentType,
                    request.Image.FileName,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogWarning("Poster replacement upload validation failed: {Message}", ex.Message);
                return (null, ex.Message, false, false, false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during poster replacement upload.");
                return (null, "Failed to upload replacement poster. Please try again.", false, false, false);
            }

            oldImageBlobName = eventItem.ImageBlobName;
            eventItem.ImageBlobName = newImageBlobName;
        }

        // Update fields
        eventItem.Title = request.Title.Trim();
        eventItem.Description = request.Description.Trim();
        eventItem.Venue = request.Venue.Trim();
        eventItem.EventDate = eventDateUtc;
        eventItem.Price = request.Price;

        // State transition: Server controls status to Pending
        eventItem.Status = EventStatus.Pending;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "DB save failed during event resubmission. EventId={EventId}", eventId);

            // If a new poster was uploaded, compensate by deleting it
            if (newImageBlobName != null)
            {
                try
                {
                    await _imageStorage.DeleteAsync(newImageBlobName, cancellationToken);
                }
                catch (Exception cleanupEx)
                {
                    _logger?.LogWarning(cleanupEx, "Failed to clean up newly uploaded blob {BlobName}", newImageBlobName);
                }
            }

            return (null, "Failed to save resubmitted event. Please try again.", false, false, false);
        }

        // DB update succeeded — best-effort cleanup of old blob if it was replaced
        if (oldImageBlobName != null)
        {
            try
            {
                await _imageStorage.DeleteAsync(oldImageBlobName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Best-effort cleanup of old blob failed: {BlobName}", oldImageBlobName);
            }
        }

        _logger?.LogInformation(
            "Event resubmitted. EventId={EventId} OrganizerId={OrganizerId} Status={Status}",
            eventItem.Id, organizerId, eventItem.Status);

        var response = new OrganizerEventSubmissionDto
        {
            Id = eventItem.Id,
            Title = eventItem.Title,
            Description = eventItem.Description,
            Venue = eventItem.Venue,
            EventDate = eventItem.EventDate,
            Price = eventItem.Price,
            Status = eventItem.Status.ToString(),
            CreatedAt = eventItem.CreatedAt,
            ImageUrl = _imageStorage.GetPublicUrl(eventItem.ImageBlobName),
            ReviewedAt = eventItem.ReviewedAt,
            ReviewComment = eventItem.ReviewComment,
        };

        return (response, null, false, false, false);
    }
}
