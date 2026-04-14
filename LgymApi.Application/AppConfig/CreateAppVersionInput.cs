using LgymApi.Domain.Enums;

namespace LgymApi.Application.AppConfig;

public sealed record CreateAppVersionInput(
    Platforms Platform,
    string? MinRequiredVersion,
    string? LatestVersion,
    bool ForceUpdate,
    string? UpdateUrl,
    string? ReleaseNotes);
