using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class AppConfig : EntityBase
{
    public Platforms Platform { get; set; }
    public string MinRequiredVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool ForceUpdate { get; set; }
    public string UpdateUrl { get; set; } = string.Empty;
    public string? ReleaseNotes { get; set; }
}
