using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Options;
using Microsoft.Extensions.Logging;

namespace LgymApi.Infrastructure.Services;

public sealed class CloudflareR2PhotoStorageProvider : IPhotoStorageProvider
{
    private readonly PhotoStorageOptions _options;
    private readonly ILogger<CloudflareR2PhotoStorageProvider> _logger;
    private readonly IAmazonS3 _client;

    public CloudflareR2PhotoStorageProvider(
        PhotoStorageOptions options,
        ILogger<CloudflareR2PhotoStorageProvider> logger)
    {
        _options = options;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto"
        };

        _client = new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey),
            config);
    }

    public Task<string> GenerateSignedUploadUrlAsync(
        string storageKey,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = storageKey,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.Add(expiration),
                ContentType = contentType
            };

            return Task.FromResult(_client.GetPreSignedURL(request));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate R2 signed upload URL for key {StorageKey}", storageKey);
            throw;
        }
    }

    public Task<string> GenerateSignedReadUrlAsync(
        string storageKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = storageKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiration)
            };

            return Task.FromResult(_client.GetPreSignedURL(request));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate R2 signed read URL for key {StorageKey}", storageKey);
            throw;
        }
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = storageKey
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("R2 delete ignored missing object {StorageKey}", storageKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete R2 object {StorageKey}", storageKey);
            throw;
        }
    }

    public async Task<PhotoMetadata?> GetMetadataAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.BucketName,
                Key = storageKey
            }, cancellationToken);

            return new PhotoMetadata
            {
                SizeBytes = response.Headers.ContentLength,
                ContentType = response.Headers.ContentType ?? "application/octet-stream",
                UploadedAt = response.LastModified.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(response.LastModified.Value, DateTimeKind.Utc))
                    : DateTimeOffset.UtcNow,
                ETag = response.ETag?.Trim('"') ?? string.Empty
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("R2 metadata lookup returned missing object for key {StorageKey}", storageKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read R2 metadata for key {StorageKey}", storageKey);
            throw;
        }
    }
}
