using System.Text.Json.Serialization;

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
    public string Platform { get; set; } = string.Empty;
}
