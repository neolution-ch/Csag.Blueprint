namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Provides an abstraction for image storage operations.
/// This allows swapping between database storage and blob storage implementations.
/// </summary>
public interface IImageStorageService
{
    /// <summary>
    /// Stores an image file and returns its metadata.
    /// </summary>
    /// <param name="stream">The image data stream.</param>
    /// <param name="fileName">The original filename.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored image metadata.</returns>
    Task<StoredImageData> StoreImageAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an image by its identifier.
    /// </summary>
    /// <param name="imageIdentifier">The identifier of the image to delete (could be ID, path, or blob name depending on implementation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteImageAsync(string imageIdentifier, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the stored image data returned by the storage service.
/// </summary>
/// <param name="Data">The binary image data (for database storage) or null (for blob storage).</param>
/// <param name="ContentType">The MIME type of the image.</param>
/// <param name="FileName">The original filename of the image.</param>
/// <param name="SizeBytes">The size of the image in bytes.</param>
/// <param name="Url">The URL to access the image (for blob storage) or null (for database storage).</param>
public record StoredImageData(
    byte[]? Data,
    string ContentType,
    string FileName,
    int SizeBytes,
    string? Url);
