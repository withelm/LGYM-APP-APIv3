using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface ISupplementationRepository
{
    Task AddPlanAsync(SupplementPlan plan, CancellationToken cancellationToken = default);
    Task<SupplementPlan?> FindPlanByIdAsync(Id<SupplementPlan> planId, CancellationToken cancellationToken = default);
    Task<List<SupplementPlan>> GetPlansByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<SupplementPlan?> GetActivePlanForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<List<SupplementIntakeLog>> GetIntakeLogsForPlanAsync(Id<User> traineeId, Id<SupplementPlan> planId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
    Task<SupplementIntakeLog?> FindIntakeLogAsync(Id<User> traineeId, Id<SupplementPlanItem> planItemId, DateOnly intakeDate, CancellationToken cancellationToken = default);
    Task AddIntakeLogAsync(SupplementIntakeLog intakeLog, CancellationToken cancellationToken = default);
}
