using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Supplementation;

public interface ISupplementationService
{
    Task<List<SupplementPlanResult>> GetTraineePlansAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<SupplementPlanResult> CreateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default);
    Task<SupplementPlanResult> UpdateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default);
    Task DeleteTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, CancellationToken cancellationToken = default);
    Task AssignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, CancellationToken cancellationToken = default);
    Task UnassignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<List<SupplementScheduleEntryResult>> GetActiveScheduleForDateAsync(UserEntity currentTrainee, DateOnly date, CancellationToken cancellationToken = default);
    Task<SupplementScheduleEntryResult> CheckOffIntakeAsync(UserEntity currentTrainee, CheckOffSupplementIntakeCommand command, CancellationToken cancellationToken = default);
    Task<SupplementComplianceSummaryResult> GetComplianceSummaryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}
