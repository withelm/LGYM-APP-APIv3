using System.Text.Json.Serialization;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.AppConfig.Contracts;

public class AppConfigInfoDto
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

public class AppConfigInfoWithPlatformDto : AppConfigInfoDto
{
    [JsonPropertyName("platform")]
    public Platforms Platform { get; set; }
}

public sealed class AppConfigVersionRequestDto
{
    [JsonPropertyName("platform")]
    public Platforms Platform { get; set; }
}
