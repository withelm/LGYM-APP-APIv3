using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.AppConfig;

public interface IAppConfigService
{
    Task<AppConfigEntity> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default);
    Task CreateNewAppVersionAsync(Guid userId, CreateAppVersionInput input, CancellationToken cancellationToken = default);
}
