using LgymApi.Application.Features.PlanDay.Models;
using LgymApi.Domain.ValueObjects;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.PlanDay;

public interface IPlanDayService
{
    Task CreatePlanDayAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Plan> planId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default);
    Task UpdatePlanDayAsync(UserEntity currentUser, Id<PlanDayEntity> planDayId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default);
    Task<PlanDayDetailsContext> GetPlanDayAsync(UserEntity currentUser, Id<PlanDayEntity> planDayId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<PlanDaysContext> GetPlanDaysAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Plan> planId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<List<PlanDayEntity>> GetPlanDaysTypesAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default);
    Task DeletePlanDayAsync(UserEntity currentUser, Id<PlanDayEntity> planDayId, CancellationToken cancellationToken = default);
    Task<PlanDaysInfoContext> GetPlanDaysInfoAsync(UserEntity currentUser, Id<LgymApi.Domain.Entities.Plan> planId, CancellationToken cancellationToken = default);
}
