using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class EloRegistryRepository : IEloRegistryRepository
{
    private readonly AppDbContext _dbContext;

    public EloRegistryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(EloRegistry registry, CancellationToken cancellationToken = default)
    {
        await _dbContext.EloRegistries.AddAsync(registry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<int?> GetLatestEloAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.EloRegistries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Date)
            .Select(e => (int?)e.Elo)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<EloRegistry?> GetLatestEntryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.EloRegistries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<EloRegistry>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.EloRegistries
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Date)
            .ToListAsync(cancellationToken);
    }
}
