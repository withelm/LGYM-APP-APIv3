using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;

namespace LgymApi.Application.Features.AppConfig;

public interface IAppConfigService
{
    Task<AppConfigEntity> GetLatestByPlatformAsync(string platformRaw);
    Task CreateNewAppVersionAsync(
        Guid userId,
        string platformRaw,
        string? minRequiredVersion,
        string? latestVersion,
        bool forceUpdate,
        string? updateUrl,
        string? releaseNotes);
}
