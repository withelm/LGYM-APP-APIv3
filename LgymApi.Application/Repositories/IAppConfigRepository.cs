using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Repositories;

public interface IAppConfigRepository
{
    Task<AppConfig?> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default);
    Task AddAsync(AppConfig config, CancellationToken cancellationToken = default);
}
