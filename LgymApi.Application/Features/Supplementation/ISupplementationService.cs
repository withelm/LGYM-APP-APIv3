using LgymApi.Application.Features.Supplementation.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Supplementation;

public interface ISupplementationService
{
    Task<List<SupplementPlanResult>> GetTraineePlansAsync(UserEntity currentTrainer, Guid traineeId);
    Task<SupplementPlanResult> CreateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, UpsertSupplementPlanCommand command);
    Task<SupplementPlanResult> UpdateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, UpsertSupplementPlanCommand command);
    Task DeleteTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId);
    Task AssignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId);
    Task UnassignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId);
    Task<List<SupplementScheduleEntryResult>> GetActiveScheduleForDateAsync(UserEntity currentTrainee, DateOnly date);
    Task<SupplementScheduleEntryResult> CheckOffIntakeAsync(UserEntity currentTrainee, CheckOffSupplementIntakeCommand command);
    Task<SupplementComplianceSummaryResult> GetComplianceSummaryAsync(UserEntity currentTrainer, Guid traineeId, DateOnly fromDate, DateOnly toDate);
}
