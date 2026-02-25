using LgymApi.Application.Features.Supplementation.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Supplementation;

public interface ISupplementationService
{
    Task<List<SupplementPlanResult>> GetTraineePlansAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
    Task<SupplementPlanResult> CreateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default);
    Task<SupplementPlanResult> UpdateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default);
    Task DeleteTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, CancellationToken cancellationToken = default);
    Task AssignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, CancellationToken cancellationToken = default);
    Task UnassignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
    Task<List<SupplementScheduleEntryResult>> GetActiveScheduleForDateAsync(UserEntity currentTrainee, DateOnly date, CancellationToken cancellationToken = default);
    Task<SupplementScheduleEntryResult> CheckOffIntakeAsync(UserEntity currentTrainee, CheckOffSupplementIntakeCommand command, CancellationToken cancellationToken = default);
    Task<SupplementComplianceSummaryResult> GetComplianceSummaryAsync(UserEntity currentTrainer, Guid traineeId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}
