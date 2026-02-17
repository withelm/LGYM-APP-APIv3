using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class PlanDayExerciseRepository : IPlanDayExerciseRepository
{
    private readonly AppDbContext _dbContext;

    public PlanDayExerciseRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<PlanDayExercise>> GetByPlanDayIdsAsync(List<Guid> planDayIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDayExercises
            .Where(e => planDayIds.Contains(e.PlanDayId))
            .OrderBy(e => e.PlanDayId)
            .ThenBy(e => e.Order)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PlanDayExercise>> GetByPlanDayIdAsync(Guid planDayId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDayExercises
            .Where(e => e.PlanDayId == planDayId)
            .OrderBy(e => e.Order)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<PlanDayExercise> exercises, CancellationToken cancellationToken = default)
    {
        var exercisesToAdd = exercises.ToList();
        if (exercisesToAdd.Count == 0)
        {
            return;
        }

        await _dbContext.PlanDayExercises.AddRangeAsync(exercisesToAdd, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await NormalizeOrdersAsync(exercisesToAdd.Select(e => e.PlanDayId), cancellationToken);
    }

    public async Task RemoveByPlanDayIdAsync(Guid planDayId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PlanDayExercises
            .Where(e => e.PlanDayId == planDayId)
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
        {
            return;
        }

        _dbContext.PlanDayExercises.RemoveRange(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await NormalizeOrdersAsync(new[] { planDayId }, cancellationToken);
    }

    private async Task NormalizeOrdersAsync(IEnumerable<Guid> planDayIds, CancellationToken cancellationToken)
    {
        var affectedPlanDayIds = planDayIds
            .Distinct()
            .ToList();

        if (affectedPlanDayIds.Count == 0)
        {
            return;
        }

        var orderedExercises = await _dbContext.PlanDayExercises
            .Where(e => affectedPlanDayIds.Contains(e.PlanDayId))
            .OrderBy(e => e.PlanDayId)
            .ThenBy(e => e.Order)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        var changed = false;
        Guid? currentPlanDayId = null;
        var nextOrder = 0;

        foreach (var exercise in orderedExercises)
        {
            if (currentPlanDayId != exercise.PlanDayId)
            {
                currentPlanDayId = exercise.PlanDayId;
                nextOrder = 0;
            }

            if (exercise.Order != nextOrder)
            {
                exercise.Order = nextOrder;
                changed = true;
            }

            nextOrder++;
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
