using LgymApi.Application.TrainingPlanning.Plan.ActivePlanPointer;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Infrastructure.Repositories;

public sealed class ActivePlanPointerStore : IActivePlanPointerStore
{
    private readonly AppDbContext _dbContext;

    public ActivePlanPointerStore(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Id<PlanEntity>?> GetActivePlanIdAsync(
        Id<UserEntity> userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
        return user?.PlanId;
    }

    public async Task StageActivePlanIdAsync(
        Id<UserEntity> userId,
        Id<PlanEntity>? planId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is not null)
        {
            user.PlanId = planId;
        }
    }
}
