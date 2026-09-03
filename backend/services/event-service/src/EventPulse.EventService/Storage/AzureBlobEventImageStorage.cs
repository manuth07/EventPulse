using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace EventPulse.EventService.Storage;

/// <summary>
/// Azure Blob Storage implementation of IEventImageStorage.
/// Uses Azurite locally; Azure Blob in production.
///
/// Container: event-posters (public-read blobs, no write access publicly).
///
/// Public-read container strategy:
///   Event posters are intended to be publicly viewable once an event is Published.
///   The container is set to public blob access (not container access).
///   This means clients can read individual blobs via URL but cannot list the container.
///   Write/delete access requires the storage account key (only held by Event Service).
/// </summary>
public class AzureBlobEventImageStorage : IEventImageStorage
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    private static readonly Dictionary<string, string> ContentTypeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        { "image/jpeg", ".jpg" },
        { "image/png",  ".png" },
        { "image/webp", ".webp" },
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobEventImageStorage> _logger;
    private readonly string _blobBaseUrl;

    private bool _containerInitialized;

    public AzureBlobEventImageStorage(
        IConfiguration configuration,
        ILogger<AzureBlobEventImageStorage> logger)
    {
        _logger = logger;

        var connectionString = configuration["BlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException(
                "BlobStorage:ConnectionString is not configured. " +
                "Local dev: add to appsettings.Development.json or user-secrets. " +
                "Production: set environment variable BlobStorage__ConnectionString.");

        var containerName = configuration["BlobStorage:ContainerName"] ?? "event-posters";

        var serviceClient = new BlobServiceClient(connectionString);
        _containerClient = serviceClient.GetBlobContainerClient(containerName);

        // Resolve the base URL for building public blob URLs.
        // For Azurite: http://127.0.0.1:10000/devstoreaccount1/event-posters
        // For Azure:   https://<account>.blob.core.windows.net/event-posters
        _blobBaseUrl = _containerClient.Uri.ToString().TrimEnd('/');
    }

    /// <inheritdoc/>
    public async Task<string> UploadAsync(
        Stream imageStream,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        // ---- Validate content type ----
        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidOperationException(
                $"Unsupported image type '{contentType}'. Allowed: JPEG, PNG, WebP.");

        // ---- Validate size ----
        if (imageStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"Image size {imageStream.Length:N0} bytes exceeds the 5 MB maximum.");

        // ---- Generate safe, collision-resistant blob name ----
        if (!ContentTypeToExtension.TryGetValue(contentType, out var ext))
            ext = ".jpg";

        var blobName = $"events/{Guid.NewGuid()}{ext}";

        // ---- Ensure container exists (lazy, idempotent) ----
        await EnsureContainerAsync(cancellationToken);

        // ---- Upload ----
        var blobClient = _containerClient.GetBlobClient(blobName);
        imageStream.Position = 0;

        await blobClient.UploadAsync(imageStream, new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Poster uploaded. BlobName={BlobName}", blobName);
        return blobName;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Poster deleted (compensating cleanup). BlobName={BlobName}", blobName);
        }
        catch (Exception ex)
        {
            // Best-effort cleanup — log and continue so the caller can report the original failure.
            _logger.LogError(ex,
                "Failed to delete orphaned blob during cleanup. BlobName={BlobName}. Manual cleanup may be required.",
                blobName);
        }
    }

    /// <inheritdoc/>
    public string? GetPublicUrl(string? blobName)
    {
        if (string.IsNullOrEmpty(blobName))
            return null;

        return $"{_blobBaseUrl}/{blobName}";
    }

    // ---------------------------------------------------------------------------

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerInitialized) return;

        // PublicAccessType.Blob: individual blobs are publicly readable by URL,
        // but listing the container requires credentials.
        await _containerClient.CreateIfNotExistsAsync(
            PublicAccessType.Blob,
            cancellationToken: cancellationToken);

        _containerInitialized = true;
    }
}
