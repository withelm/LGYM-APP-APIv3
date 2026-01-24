using LgymApi.Application.Models;
using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default);
    Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
