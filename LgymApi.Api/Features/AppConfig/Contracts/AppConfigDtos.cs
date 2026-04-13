using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.AppConfig.Contracts;

public sealed class AppConfigInfoDto : IResultDto
{
    [JsonPropertyName("minRequiredVersion")]
    public string MinRequiredVersion { get; set; } = string.Empty;

    [JsonPropertyName("latestVersion")]
    public string LatestVersion { get; set; } = string.Empty;

    [JsonPropertyName("forceUpdate")]
    public bool ForceUpdate { get; set; }

    [JsonPropertyName("updateUrl")]
    public string UpdateUrl { get; set; } = string.Empty;

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }
}

public sealed class AppConfigInfoWithPlatformDto : IDto, IResultDto
{
    [JsonPropertyName("minRequiredVersion")]
    public string MinRequiredVersion { get; set; } = string.Empty;

    [JsonPropertyName("latestVersion")]
    public string LatestVersion { get; set; } = string.Empty;

    [JsonPropertyName("forceUpdate")]
    public bool ForceUpdate { get; set; }

    [JsonPropertyName("updateUrl")]
    public string UpdateUrl { get; set; } = string.Empty;

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("platform")]
    public Platforms Platform { get; set; }
}

public sealed class AppConfigVersionRequestDto : IDto
{
    [JsonPropertyName("platform")]
    [JsonRequired]
    public Platforms Platform { get; set; }
}

public sealed class AppConfigDetailDto : IResultDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public Platforms Platform { get; set; }

    [JsonPropertyName("minRequiredVersion")]
    public string MinRequiredVersion { get; set; } = string.Empty;

    [JsonPropertyName("latestVersion")]
    public string LatestVersion { get; set; } = string.Empty;

    [JsonPropertyName("forceUpdate")]
    public bool ForceUpdate { get; set; }

    [JsonPropertyName("updateUrl")]
    public string UpdateUrl { get; set; } = string.Empty;

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class UpdateAppConfigRequest : IDto
{
    [JsonPropertyName("platform")]
    public Platforms Platform { get; set; }

    [JsonPropertyName("minRequiredVersion")]
    public string? MinRequiredVersion { get; set; }

    [JsonPropertyName("latestVersion")]
    public string? LatestVersion { get; set; }

    [JsonPropertyName("forceUpdate")]
    public bool ForceUpdate { get; set; }

    [JsonPropertyName("updateUrl")]
    public string? UpdateUrl { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }
}

public sealed class PaginatedAppConfigRequest : PaginatedRequest, IDto
{
}

public sealed class PaginatedAppConfigResult : PaginatedResponse<AppConfigDetailDto>, IResultDto
{
}
