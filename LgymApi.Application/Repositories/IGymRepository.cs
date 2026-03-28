using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IGymRepository
{
    Task AddAsync(Gym gym, CancellationToken cancellationToken = default);
    Task<Gym?> FindByIdAsync(Id<Gym> id, CancellationToken cancellationToken = default);
    Task<List<Gym>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Gym gym, CancellationToken cancellationToken = default);
}
