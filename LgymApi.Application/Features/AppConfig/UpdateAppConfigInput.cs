using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.AppConfig;

public sealed record UpdateAppConfigInput(
    Platforms Platform,
    string? MinRequiredVersion,
    string? LatestVersion,
    bool ForceUpdate,
    string? UpdateUrl,
    string? ReleaseNotes);
