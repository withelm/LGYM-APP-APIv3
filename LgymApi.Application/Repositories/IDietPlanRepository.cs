using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IDietPlanRepository
{
    Task AddPlanAsync(DietPlan plan, CancellationToken cancellationToken = default);
    Task AddHistoryEntryAsync(DietPlanHistory historyEntry, CancellationToken cancellationToken = default);
    Task<DietPlan?> FindPlanByIdAsync(Id<DietPlan> planId, CancellationToken cancellationToken = default);
    Task<List<DietPlan>> GetPlansByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<List<DietPlan>> GetActivePlansForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<DietPlan?> GetActivePlanForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<List<DietPlanHistory>> GetPlanHistoryAsync(Id<DietPlan> planId, CancellationToken cancellationToken = default);
}
