using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IGymRepository
{
    Task AddAsync(Gym gym, CancellationToken cancellationToken = default);
    Task<Gym?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Gym>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Gym gym, CancellationToken cancellationToken = default);
}
