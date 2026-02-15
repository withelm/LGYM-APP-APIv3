using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class PlanDayRepository : IPlanDayRepository
{
    private readonly AppDbContext _dbContext;

    public PlanDayRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PlanDay?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDays.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<List<PlanDay>> GetByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDays.Where(p => p.PlanId == planId && !p.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PlanDay planDay, CancellationToken cancellationToken = default)
    {
        await _dbContext.PlanDays.AddAsync(planDay, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PlanDay planDay, CancellationToken cancellationToken = default)
    {
        _dbContext.PlanDays.Update(planDay);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkDeletedAsync(Guid planDayId, CancellationToken cancellationToken = default)
    {
        await _dbContext.PlanDays
            .Where(p => p.Id == planDayId)
            .ExecuteUpdateAsync(_dbContext, p => p.IsDeleted, p => true, cancellationToken);
    }

    public async Task MarkDeletedByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await _dbContext.PlanDays
            .Where(p => p.PlanId == planId)
            .ExecuteUpdateAsync(_dbContext, p => p.IsDeleted, p => true, cancellationToken);
    }

    public Task<bool> AnyByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDays.AnyAsync(p => p.PlanId == planId && !p.IsDeleted, cancellationToken);
    }
}
