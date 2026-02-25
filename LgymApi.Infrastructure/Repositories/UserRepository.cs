using LgymApi.Application.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<User?> FindByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Name == name, cancellationToken);
    }

    public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Name == name || u.Email == email, cancellationToken);
    }

    public async Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default)
    {
        var rankedUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.IsVisibleInRanking)
            .Where(u => !u.UserRoles.Any(ur => ur.RoleId == AppDbContext.TesterRoleSeedId))
            .Select(u => new
            {
                User = u,
                Elo = _dbContext.EloRegistries
                    .Where(e => e.UserId == u.Id)
                    .OrderByDescending(e => e.Date)
                    .Select(e => (int?)e.Elo)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rankedUsers
            .Select(entry => new UserRankingEntry(entry.User, entry.Elo ?? 1000))
            .OrderByDescending(entry => entry.Elo)
            .ToList();
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AddAsync(user, cancellationToken).AsTask();
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Update(user);
        return Task.CompletedTask;
    }
}
