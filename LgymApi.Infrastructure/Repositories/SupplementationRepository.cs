using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class SupplementationRepository : ISupplementationRepository
{
    private readonly AppDbContext _dbContext;

    public SupplementationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddPlanAsync(SupplementPlan plan, CancellationToken cancellationToken = default)
    {
        await _dbContext.SupplementPlans.AddAsync(plan, cancellationToken);
    }

    public Task<SupplementPlan?> FindPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SupplementPlans
            .Include(x => x.Items.OrderBy(i => i.Order).ThenBy(i => i.TimeOfDay).ThenBy(i => i.CreatedAt))
            .FirstOrDefaultAsync(x => x.Id == planId, cancellationToken);
    }

    public Task<List<SupplementPlan>> GetPlansByTrainerAndTraineeAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SupplementPlans
            .Where(x => x.TrainerId == trainerId && x.TraineeId == traineeId && !x.IsDeleted)
            .Include(x => x.Items.OrderBy(i => i.Order).ThenBy(i => i.TimeOfDay).ThenBy(i => i.CreatedAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<SupplementPlan?> GetActivePlanForTraineeAsync(Guid traineeId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SupplementPlans
            .Where(x => x.TraineeId == traineeId && x.IsActive && !x.IsDeleted)
            .Include(x => x.Items.OrderBy(i => i.Order).ThenBy(i => i.TimeOfDay).ThenBy(i => i.CreatedAt))
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<SupplementIntakeLog>> GetIntakeLogsForPlanAsync(Guid traineeId, Guid planId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        return _dbContext.SupplementIntakeLogs
            .Where(x => x.TraineeId == traineeId
                        && x.PlanItem.PlanId == planId
                        && x.IntakeDate >= fromDate
                        && x.IntakeDate <= toDate)
            .Include(x => x.PlanItem)
            .OrderBy(x => x.IntakeDate)
            .ThenBy(x => x.TakenAt)
            .ToListAsync(cancellationToken);
    }

    public Task<SupplementIntakeLog?> FindIntakeLogAsync(Guid traineeId, Guid planItemId, DateOnly intakeDate, CancellationToken cancellationToken = default)
    {
        return _dbContext.SupplementIntakeLogs
            .FirstOrDefaultAsync(x => x.TraineeId == traineeId && x.PlanItemId == planItemId && x.IntakeDate == intakeDate, cancellationToken);
    }

    public async Task AddIntakeLogAsync(SupplementIntakeLog intakeLog, CancellationToken cancellationToken = default)
    {
        await _dbContext.SupplementIntakeLogs.AddAsync(intakeLog, cancellationToken);
    }
}
