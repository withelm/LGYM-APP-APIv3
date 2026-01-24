using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class GymRepository : IGymRepository
{
    private readonly AppDbContext _dbContext;

    public GymRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Gym gym, CancellationToken cancellationToken = default)
    {
        await _dbContext.Gyms.AddAsync(gym, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Gym?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Gyms.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public Task<List<Gym>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Gyms
            .Where(g => g.UserId == userId && !g.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Gym gym, CancellationToken cancellationToken = default)
    {
        _dbContext.Gyms.Update(gym);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
