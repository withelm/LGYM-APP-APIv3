using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.AppConfig;

public sealed record CreateAppVersionInput(
    Platforms Platform,
    string? MinRequiredVersion,
    string? LatestVersion,
    bool ForceUpdate,
    string? UpdateUrl,
    string? ReleaseNotes);
