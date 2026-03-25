using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
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

    public Task<PlanDay?> FindByIdAsync(Id<PlanDay> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDays.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<List<PlanDay>> GetByPlanIdAsync(Id<Plan> planId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDays.AsNoTracking().Where(p => p.PlanId == planId && !p.IsDeleted).ToListAsync(cancellationToken);
    }

    public Task AddAsync(PlanDay planDay, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDays.AddAsync(planDay, cancellationToken).AsTask();
    }

    public Task UpdateAsync(PlanDay planDay, CancellationToken cancellationToken = default)
    {
        _dbContext.PlanDays.Update(planDay);
        return Task.CompletedTask;
    }

    public async Task MarkDeletedAsync(Id<PlanDay> planDayId, CancellationToken cancellationToken = default)
    {
        await _dbContext.PlanDays
            .Where(p => p.Id == planDayId)
            .StageUpdateAsync(_dbContext, p => p.IsDeleted, p => true, cancellationToken);
    }

    public async Task MarkDeletedByPlanIdAsync(Id<Plan> planId, CancellationToken cancellationToken = default)
    {
        await _dbContext.PlanDays
            .Where(p => p.PlanId == planId)
            .StageUpdateAsync(_dbContext, p => p.IsDeleted, p => true, cancellationToken);
    }

    public Task<bool> AnyByPlanIdAsync(Id<Plan> planId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDays.AnyAsync(p => p.PlanId == planId && !p.IsDeleted, cancellationToken);
    }
}
