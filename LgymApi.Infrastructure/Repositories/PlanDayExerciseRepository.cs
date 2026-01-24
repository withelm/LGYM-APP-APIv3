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
            .ToListAsync(cancellationToken);
    }

    public Task<List<PlanDayExercise>> GetByPlanDayIdAsync(Guid planDayId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PlanDayExercises
            .Where(e => e.PlanDayId == planDayId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<PlanDayExercise> exercises, CancellationToken cancellationToken = default)
    {
        await _dbContext.PlanDayExercises.AddRangeAsync(exercises, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveByPlanDayIdAsync(Guid planDayId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PlanDayExercises
            .Where(e => e.PlanDayId == planDayId)
            .ToListAsync(cancellationToken);

        _dbContext.PlanDayExercises.RemoveRange(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
