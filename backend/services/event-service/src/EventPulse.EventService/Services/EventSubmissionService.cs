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
    private readonly ILogger<EventSubmissionService> _logger;

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
        ILogger<EventSubmissionService> logger)
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
            _logger.LogWarning("Poster upload validation failed: {Message}", ex.Message);
            return (null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during poster upload.");
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
            _logger.LogError(ex,
                "DB save failed after poster upload. Attempting compensating blob deletion. BlobName={BlobName}",
                imageBlobName);

            // Compensating cleanup: remove the orphaned blob
            await _imageStorage.DeleteAsync(imageBlobName, cancellationToken);

            return (null, "Failed to save event. Please try again.");
        }

        _logger.LogInformation(
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
}
