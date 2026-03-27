using AppConfigEntity = LgymApi.Domain.Entities.AppConfig;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.AppConfig;

public interface IAppConfigService
{
    Task<AppConfigEntity> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default);
    Task CreateNewAppVersionAsync(Id<LgymApi.Domain.Entities.User> userId, CreateAppVersionInput input, CancellationToken cancellationToken = default);
}
