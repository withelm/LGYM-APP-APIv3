using LgymApi.Application.Abstractions.Storage;
using Microsoft.AspNetCore.Http;

namespace LgymApi.Infrastructure.Services;

/// <summary>
/// Local development implementation of IPhotoStorageProvider.
/// Returns placeholder signed URLs for dev/test environments.
/// NOT FOR PRODUCTION USE - implement CloudflareR2PhotoStorageProvider or SupabasePhotoStorageProvider for production.
/// </summary>
public sealed class LocalPhotoStorageProvider : IPhotoStorageProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LocalPhotoDevelopmentStore _store;

    public LocalPhotoStorageProvider(IHttpContextAccessor httpContextAccessor, LocalPhotoDevelopmentStore store)
    {
        _httpContextAccessor = httpContextAccessor;
        _store = store;
    }

    public Task<string> GenerateSignedUploadUrlAsync(
        string storageKey,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var placeholderUrl = $"{GetBaseUrl()}/dev/photos/upload/{Uri.EscapeDataString(storageKey)}?expires={DateTimeOffset.UtcNow.Add(expiration):O}";
        return Task.FromResult(placeholderUrl);
    }

    public Task<string> GenerateSignedReadUrlAsync(
        string storageKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var placeholderUrl = $"{GetBaseUrl()}/dev/photos/read/{Uri.EscapeDataString(storageKey)}?expires={DateTimeOffset.UtcNow.Add(expiration):O}";
        return Task.FromResult(placeholderUrl);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        return _store.DeleteAsync(storageKey, cancellationToken);
    }

    public Task<PhotoMetadata?> GetMetadataAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        return _store.GetMetadataAsync(storageKey, cancellationToken);
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null && request.Host.HasValue)
        {
            return $"{request.Scheme}://{request.Host.Value}";
        }

        return "https://localhost:7025";
    }
}
