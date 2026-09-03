namespace EventPulse.EventService.Storage;

/// <summary>
/// Abstracts event poster blob operations from the submission service.
/// Implementations: AzureBlobEventImageStorage (Azurite locally, Azure Blob production).
/// </summary>
public interface IEventImageStorage
{
    /// <summary>
    /// Validates and uploads an image stream to blob storage.
    /// Returns the generated blob name (e.g. "events/abc123.jpg") on success.
    /// Throws InvalidOperationException for type/size violations.
    /// </summary>
    Task<string> UploadAsync(Stream imageStream, string contentType, string originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob by its stored reference key.
    /// Best-effort — does not throw if blob does not exist.
    /// </summary>
    Task DeleteAsync(string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the publicly accessible URL for a given blob name.
    /// Returns null if blobName is null.
    /// </summary>
    string? GetPublicUrl(string? blobName);
}
