using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IEloRegistryRepository
{
    Task AddAsync(EloRegistry registry, CancellationToken cancellationToken = default);
    Task<int?> GetLatestEloAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<EloRegistry?> GetLatestEntryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<EloRegistry>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
