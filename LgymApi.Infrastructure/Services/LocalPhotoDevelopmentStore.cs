using Microsoft.AspNetCore.StaticFiles;

namespace LgymApi.Infrastructure.Services;

public sealed class LocalPhotoDevelopmentStore
{
    private readonly string _rootPath;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public LocalPhotoDevelopmentStore()
    {
        _rootPath = Path.Combine(AppContext.BaseDirectory, "dev-photo-storage");
    }

    public string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Storage key is required.", nameof(storageKey));
        }

        var normalizedSegments = storageKey
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .ToArray();

        if (normalizedSegments.Length == 0 || normalizedSegments.Any(segment => segment == "." || segment == ".."))
        {
            throw new InvalidOperationException("Invalid storage key path.");
        }

        return Path.Combine(new[] { _rootPath }.Concat(normalizedSegments).ToArray());
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<LgymApi.Application.Abstractions.Storage.PhotoMetadata?> GetMetadataAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<LgymApi.Application.Abstractions.Storage.PhotoMetadata?>(null);
        }

        var fileInfo = new FileInfo(path);
        _contentTypeProvider.TryGetContentType(path, out var contentType);

        return Task.FromResult<LgymApi.Application.Abstractions.Storage.PhotoMetadata?>(new LgymApi.Application.Abstractions.Storage.PhotoMetadata
        {
            SizeBytes = fileInfo.Length,
            ContentType = contentType ?? "application/octet-stream",
            UploadedAt = fileInfo.CreationTimeUtc,
            ETag = fileInfo.LastWriteTimeUtc.Ticks.ToString()
        });
    }

    public string ResolveContentType(string storageKey)
    {
        var path = ResolvePath(storageKey);
        return _contentTypeProvider.TryGetContentType(path, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
