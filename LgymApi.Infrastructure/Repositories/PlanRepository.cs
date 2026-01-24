using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class PlanRepository : IPlanRepository
{
    private readonly AppDbContext _dbContext;

    public PlanRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Plan?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<Plan?> FindActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive, cancellationToken);
    }

    public Task<List<Plan>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Plans.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        await _dbContext.Plans.AddAsync(plan, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default)
    {
        _dbContext.Plans.Update(plan);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActivePlanAsync(Guid userId, Guid planId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Plans
            .Where(p => p.UserId == userId && p.Id != planId)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.IsActive, false), cancellationToken);

        await _dbContext.Plans
            .Where(p => p.UserId == userId && p.Id == planId)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.IsActive, true), cancellationToken);
    }
}
