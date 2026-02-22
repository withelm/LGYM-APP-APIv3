using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface ISupplementationRepository
{
    Task AddPlanAsync(SupplementPlan plan, CancellationToken cancellationToken = default);
    Task<SupplementPlan?> FindPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<List<SupplementPlan>> GetPlansByTrainerAndTraineeAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default);
    Task<SupplementPlan?> GetActivePlanForTraineeAsync(Guid traineeId, CancellationToken cancellationToken = default);
    Task<List<SupplementIntakeLog>> GetIntakeLogsForPlanAsync(Guid traineeId, Guid planId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
    Task<SupplementIntakeLog?> FindIntakeLogAsync(Guid traineeId, Guid planItemId, DateOnly intakeDate, CancellationToken cancellationToken = default);
    Task AddIntakeLogAsync(SupplementIntakeLog intakeLog, CancellationToken cancellationToken = default);
}
