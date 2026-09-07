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

    public static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Musical Concert",
        "Conference",
        "Workshop",
        "Festival",
        "Sports",
        "Theatre / Performance",
        "Other",
    };

    public static readonly HashSet<string> AllowedVenueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Indoor",
        "Outdoor",
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

        var eventDateUtc = request.EventDate.Kind == DateTimeKind.Utc
            ? request.EventDate
            : (request.EventDate.Kind == DateTimeKind.Local
                ? request.EventDate.ToUniversalTime()
                : DateTime.SpecifyKind(request.EventDate, DateTimeKind.Utc));

        if (eventDateUtc <= DateTime.UtcNow)
            return (null, "EventDate must be in the future.");

        if (request.Price < 0)
            return (null, "Price must be 0 or greater.");

        if (request.Price != decimal.Truncate(request.Price))
            return (null, "Ticket price must be entered in whole LKR.");

        string? canonicalCategory = null;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (!AllowedCategories.Contains(request.Category.Trim()))
                return (null, $"Unsupported event category '{request.Category}'.");

            canonicalCategory = AllowedCategories.First(c => string.Equals(c, request.Category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        string? canonicalVenueType = null;
        if (!string.IsNullOrWhiteSpace(request.VenueType))
        {
            if (!AllowedVenueTypes.Contains(request.VenueType.Trim()))
                return (null, $"Unsupported venue type '{request.VenueType}'. Supported values: Indoor, Outdoor.");

            canonicalVenueType = AllowedVenueTypes.First(v => string.Equals(v, request.VenueType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // ---- Poster Validation -----------------------------------------------
        if (request.Image is null || request.Image.Length == 0)
            return (null, "Event poster image is required.");

        var posterContentType = request.Image.ContentType?.Trim() ?? string.Empty;
        if (!AllowedContentTypes.Contains(posterContentType))
            return (null, $"Unsupported poster image type '{posterContentType}'. Accepted: JPEG, PNG, WebP.");

        if (request.Image.Length > MaxFileSizeBytes)
            return (null, $"Poster image exceeds the 5 MB maximum (received {request.Image.Length:N0} bytes).");

        // ---- Cover Validation ------------------------------------------------
        if (request.CoverImage is null || request.CoverImage.Length == 0)
            return (null, "Event cover image is required.");

        var coverContentType = request.CoverImage.ContentType?.Trim() ?? string.Empty;
        if (!AllowedContentTypes.Contains(coverContentType))
            return (null, $"Unsupported cover image type '{coverContentType}'. Accepted: JPEG, PNG, WebP.");

        if (request.CoverImage.Length > MaxFileSizeBytes)
            return (null, $"Cover image exceeds the 5 MB maximum (received {request.CoverImage.Length:N0} bytes).");

        // ---- Upload Poster ---------------------------------------------------
        string imageBlobName;
        try
        {
            await using var stream = request.Image.OpenReadStream();
            imageBlobName = await _imageStorage.UploadAsync(
                stream,
                posterContentType,
                request.Image.FileName,
                "event-posters",
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

        // ---- Upload Cover (Compensates Poster on failure) ---------------------
        string coverBlobName;
        try
        {
            await using var stream = request.CoverImage.OpenReadStream();
            coverBlobName = await _imageStorage.UploadAsync(
                stream,
                coverContentType,
                request.CoverImage.FileName,
                "event-covers",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cover upload failed. Performing compensating deletion of poster blob {BlobName}", imageBlobName);
            await _imageStorage.DeleteAsync(imageBlobName, cancellationToken);

            if (ex is InvalidOperationException)
                return (null, ex.Message);

            return (null, "Failed to upload event cover banner. Please try again.");
        }

        // ---- Persist Event ---------------------------------------------------
        var newEvent = new Event
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Venue = request.Venue.Trim(),
            EventDate = eventDateUtc,
            Price = request.Price,
            Category = canonicalCategory,
            VenueType = canonicalVenueType,
            // Server-controlled — never from frontend
            Status = EventStatus.Pending,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            ImageBlobName = imageBlobName,
            CoverBlobName = coverBlobName,
        };

        _context.Events.Add(newEvent);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "DB save failed after image uploads. Attempting compensating blob deletions. PosterBlobName={PosterBlob} CoverBlobName={CoverBlob}",
                imageBlobName, coverBlobName);

            // Compensating cleanup: remove both newly uploaded blobs
            await _imageStorage.DeleteAsync(imageBlobName, cancellationToken);
            await _imageStorage.DeleteAsync(coverBlobName, cancellationToken);

            return (null, "Failed to save event. Please try again.");
        }

        _logger?.LogInformation(
            "Event submitted. EventId={EventId} OrganizerId={OrganizerId} Status={Status} ImageBlobName={BlobName} CoverBlobName={CoverBlob}",
            newEvent.Id, organizerId, newEvent.Status, imageBlobName, coverBlobName);

        var response = new EventSubmissionResponseDto
        {
            Id = newEvent.Id,
            Title = newEvent.Title,
            Description = newEvent.Description,
            Venue = newEvent.Venue,
            EventDate = newEvent.EventDate,
            Price = newEvent.Price,
            Status = newEvent.Status.ToString(),
            Category = newEvent.Category,
            VenueType = newEvent.VenueType,
            OrganizerId = newEvent.OrganizerId,
            CreatedAt = newEvent.CreatedAt,
            ImageUrl = _imageStorage.GetPublicUrl(imageBlobName),
            CoverUrl = _imageStorage.GetPublicUrl(coverBlobName),
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
            Category = e.Category,
            VenueType = e.VenueType,
            CreatedAt = e.CreatedAt,
            ImageUrl = _imageStorage.GetPublicUrl(e.ImageBlobName),
            CoverUrl = _imageStorage.GetPublicUrl(e.CoverBlobName),
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
            Category = eventItem.Category,
            VenueType = eventItem.VenueType,
            CreatedAt = eventItem.CreatedAt,
            ImageUrl = _imageStorage.GetPublicUrl(eventItem.ImageBlobName),
            CoverUrl = _imageStorage.GetPublicUrl(eventItem.CoverBlobName),
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

        if (request.Price != decimal.Truncate(request.Price))
            return (null, "Ticket price must be entered in whole LKR.", false, false, false);

        var canonicalResubmitCategory = eventItem.Category;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (!AllowedCategories.Contains(request.Category.Trim()))
                return (null, $"Unsupported event category '{request.Category}'.", false, false, false);

            canonicalResubmitCategory = AllowedCategories.First(c => string.Equals(c, request.Category.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var canonicalResubmitVenueType = eventItem.VenueType;
        if (!string.IsNullOrWhiteSpace(request.VenueType))
        {
            if (!AllowedVenueTypes.Contains(request.VenueType.Trim()))
                return (null, $"Unsupported venue type '{request.VenueType}'. Supported values: Indoor, Outdoor.", false, false, false);

            canonicalResubmitVenueType = AllowedVenueTypes.First(v => string.Equals(v, request.VenueType.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        string? oldImageBlobName = null;
        string? newImageBlobName = null;
        string? oldCoverBlobName = null;
        string? newCoverBlobName = null;

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
                    "event-posters",
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
        }

        // Cover replacement if new cover is provided
        if (request.CoverImage is not null && request.CoverImage.Length > 0)
        {
            var contentType = request.CoverImage.ContentType?.Trim() ?? string.Empty;
            if (!AllowedContentTypes.Contains(contentType))
            {
                if (newImageBlobName != null)
                    await _imageStorage.DeleteAsync(newImageBlobName, cancellationToken);
                return (null, $"Unsupported cover image type '{contentType}'. Accepted: JPEG, PNG, WebP.", false, false, false);
            }

            if (request.CoverImage.Length > MaxFileSizeBytes)
            {
                if (newImageBlobName != null)
                    await _imageStorage.DeleteAsync(newImageBlobName, cancellationToken);
                return (null, $"Cover image exceeds the 5 MB maximum (received {request.CoverImage.Length:N0} bytes).", false, false, false);
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
                _logger?.LogError(ex, "Cover replacement upload failed.");
                if (newImageBlobName != null)
                    await _imageStorage.DeleteAsync(newImageBlobName, cancellationToken);

                if (ex is InvalidOperationException)
                    return (null, ex.Message, false, false, false);

                return (null, "Failed to upload replacement cover banner. Please try again.", false, false, false);
            }
        }

        // Update entity blob references if replacements occurred
        if (newImageBlobName != null)
        {
            oldImageBlobName = eventItem.ImageBlobName;
            eventItem.ImageBlobName = newImageBlobName;
        }

        if (newCoverBlobName != null)
        {
            oldCoverBlobName = eventItem.CoverBlobName;
            eventItem.CoverBlobName = newCoverBlobName;
        }

        // Update fields
        eventItem.Title = request.Title.Trim();
        eventItem.Description = request.Description.Trim();
        eventItem.Venue = request.Venue.Trim();
        eventItem.EventDate = eventDateUtc;
        eventItem.Price = request.Price;
        eventItem.Category = canonicalResubmitCategory;
        eventItem.VenueType = canonicalResubmitVenueType;

        // State transition: Server controls status to Pending
        eventItem.Status = EventStatus.Pending;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "DB save failed during event resubmission. EventId={EventId}", eventId);

            // Compensate by deleting newly uploaded blobs; keep old blobs intact!
            if (newImageBlobName != null)
            {
                try
                {
                    await _imageStorage.DeleteAsync(newImageBlobName, cancellationToken);
                }
                catch (Exception cleanupEx)
                {
                    _logger?.LogWarning(cleanupEx, "Failed to clean up newly uploaded poster blob {BlobName}", newImageBlobName);
                }
            }

            if (newCoverBlobName != null)
            {
                try
                {
                    await _imageStorage.DeleteAsync(newCoverBlobName, cancellationToken);
                }
                catch (Exception cleanupEx)
                {
                    _logger?.LogWarning(cleanupEx, "Failed to clean up newly uploaded cover blob {BlobName}", newCoverBlobName);
                }
            }

            return (null, "Failed to save resubmitted event. Please try again.", false, false, false);
        }

        // DB update succeeded — best-effort cleanup of old blobs if they were replaced
        if (oldImageBlobName != null)
        {
            try
            {
                await _imageStorage.DeleteAsync(oldImageBlobName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Best-effort cleanup of old poster blob failed: {BlobName}", oldImageBlobName);
            }
        }

        if (oldCoverBlobName != null)
        {
            try
            {
                await _imageStorage.DeleteAsync(oldCoverBlobName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Best-effort cleanup of old cover blob failed: {BlobName}", oldCoverBlobName);
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
            Category = eventItem.Category,
            VenueType = eventItem.VenueType,
            CreatedAt = eventItem.CreatedAt,
            ImageUrl = _imageStorage.GetPublicUrl(eventItem.ImageBlobName),
            CoverUrl = _imageStorage.GetPublicUrl(eventItem.CoverBlobName),
            ReviewedAt = eventItem.ReviewedAt,
            ReviewComment = eventItem.ReviewComment,
        };

        return (response, null, false, false, false);
    }
}
