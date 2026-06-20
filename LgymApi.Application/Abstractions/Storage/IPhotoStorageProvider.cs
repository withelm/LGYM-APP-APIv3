using System;
using System.Threading;
using System.Threading.Tasks;

namespace LgymApi.Application.Abstractions.Storage;

/// <summary>
/// Abstraction for cloud storage providers (S3, R2, Supabase, Azure Blob, etc.).
/// Provides signed URL generation, metadata retrieval, and deletion operations.
/// All operations are asynchronous and support cancellation tokens.
/// </summary>
public interface IPhotoStorageProvider
{
    /// <summary>
    /// Generates a signed URL for uploading a photo to cloud storage.
    /// The URL is valid for a limited time (typically 15 minutes) and includes
    /// the storage key, content-type, and size constraints.
    /// </summary>
    /// <param name="storageKey">
    /// The storage key (path) where the photo will be stored.
    /// Format: photos/{traineeId}/{reportRequestId}/{viewType}/{timestamp}-{uuid}.{ext}
    /// </param>
    /// <param name="expiration">
    /// How long the signed URL is valid (e.g., TimeSpan.FromMinutes(15)).
    /// After expiration, the URL becomes invalid and cannot be used for upload.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A signed URL string that can be used to upload the photo.
    /// The URL includes authentication credentials and constraints.
    /// </returns>
    /// <remarks>
    /// Security: The signed URL should enforce:
    /// - Content-Type validation (only image/jpeg, image/png, image/heic)
    /// - File size limit (max 10MB)
    /// - Single-use or limited-use semantics (provider-dependent)
    /// </remarks>
    Task<string> GenerateSignedUploadUrlAsync(
        string storageKey,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a signed URL for downloading/viewing a photo from cloud storage.
    /// The URL is valid for a limited time (typically 1 hour) and includes
    /// the storage key and access credentials.
    /// </summary>
    /// <param name="storageKey">
    /// The storage key (path) of the photo to download.
    /// Must match a previously uploaded photo.
    /// </param>
    /// <param name="expiration">
    /// How long the signed URL is valid (e.g., TimeSpan.FromHours(1)).
    /// After expiration, the URL becomes invalid and cannot be used for download.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A signed URL string that can be used to download the photo.
    /// The URL includes authentication credentials.
    /// </returns>
    /// <remarks>
    /// Security: The signed URL should enforce:
    /// - Read-only access (no modification or deletion)
    /// - Expiration enforcement (provider validates timestamp)
    /// - Optional: IP-based restrictions (future enhancement)
    /// </remarks>
    Task<string> GenerateSignedReadUrlAsync(
        string storageKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a photo from cloud storage.
    /// This is a hard delete operation; the photo cannot be recovered.
    /// </summary>
    /// <param name="storageKey">
    /// The storage key (path) of the photo to delete.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A completed task. If the photo does not exist, the operation succeeds silently
    /// (idempotent behavior).
    /// </returns>
    /// <remarks>
    /// Idempotency: Deleting a non-existent photo should not throw an error.
    /// This allows safe retry logic without additional existence checks.
    /// </remarks>
    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves metadata about a stored photo without downloading the binary data.
    /// Metadata includes size, content-type, upload timestamp, and ETag.
    /// </summary>
    /// <param name="storageKey">
    /// The storage key (path) of the photo.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A PhotoMetadata object containing size, content-type, and timestamps.
    /// Returns null if the photo does not exist.
    /// </returns>
    /// <remarks>
    /// Use Case: Verify photo existence, check file size, validate content-type
    /// without downloading the full binary data.
    /// </remarks>
    Task<PhotoMetadata?> GetMetadataAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata about a stored photo.
/// </summary>
public sealed class PhotoMetadata
{
    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Content-Type (e.g., "image/jpeg").</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>When the photo was uploaded (UTC).</summary>
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>ETag or version identifier for cache validation.</summary>
    public string ETag { get; set; } = string.Empty;
}
