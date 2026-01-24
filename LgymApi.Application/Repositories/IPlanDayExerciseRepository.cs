using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IPlanDayExerciseRepository
{
    Task<List<PlanDayExercise>> GetByPlanDayIdsAsync(List<Guid> planDayIds, CancellationToken cancellationToken = default);
    Task<List<PlanDayExercise>> GetByPlanDayIdAsync(Guid planDayId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<PlanDayExercise> exercises, CancellationToken cancellationToken = default);
    Task RemoveByPlanDayIdAsync(Guid planDayId, CancellationToken cancellationToken = default);
}
