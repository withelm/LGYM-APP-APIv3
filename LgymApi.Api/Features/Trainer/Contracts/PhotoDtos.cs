using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class InitiatePhotoUploadRequest : IDto
{
    [JsonPropertyName("reportRequestId")]
    public string ReportRequestId { get; set; } = string.Empty;

    [JsonPropertyName("viewType")]
    public string ViewType { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}

public sealed class InitiatePhotoUploadResponse : IResultDto
{
    [JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; } = string.Empty;

    [JsonPropertyName("storageKey")]
    public string StorageKey { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class GetSignedReadUrlResponse : IResultDto
{
    [JsonPropertyName("readUrl")]
    public string ReadUrl { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class CompletePhotoUploadRequest : IDto
{
    [JsonPropertyName("storageKey")]
    public string StorageKey { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("checksum")]
    public string Checksum { get; set; } = string.Empty;

    [JsonPropertyName("reportRequestId")]
    public string ReportRequestId { get; set; } = string.Empty;

    [JsonPropertyName("viewType")]
    public string ViewType { get; set; } = string.Empty;
}

public sealed class CompletePhotoUploadResponse : IResultDto
{
    [JsonPropertyName("photoId")]
    public string PhotoId { get; set; } = string.Empty;

    [JsonPropertyName("uploadedAt")]
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed class PhotoHistoryItemResponse : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("viewType")]
    public string ViewType { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("readUrl")]
    public string ReadUrl { get; set; } = string.Empty;

    [JsonPropertyName("reportRequestId")]
    public string ReportRequestId { get; set; } = string.Empty;

    [JsonPropertyName("uploadedAt")]
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed class GetPhotoHistoryResponse : IResultDto
{
    [JsonPropertyName("photos")]
    public List<PhotoHistoryItemResponse> Photos { get; set; } = [];
}
