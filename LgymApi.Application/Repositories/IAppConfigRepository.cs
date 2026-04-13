using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IAppConfigRepository
{
    Task<AppConfig?> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default);
    Task AddAsync(AppConfig config, CancellationToken cancellationToken = default);
    Task<AppConfig?> FindByIdAsync(Id<AppConfig> id, CancellationToken cancellationToken = default);
    Task<Pagination<AppConfig>> GetPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default);
    void Update(AppConfig config);
    void Delete(AppConfig config);
}
