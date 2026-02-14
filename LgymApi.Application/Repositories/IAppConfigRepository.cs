using LgymApi.Domain.Enums;

namespace LgymApi.Application.Repositories;

public interface IAppConfigRepository
{
    Task<global::LgymApi.Domain.Entities.AppConfig?> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default);
    Task AddAsync(global::LgymApi.Domain.Entities.AppConfig config, CancellationToken cancellationToken = default);
}
