using LgymApi.Application.Features.PlanDay.Models;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.PlanDay;

public interface IPlanDayService
{
    Task CreatePlanDayAsync(UserEntity currentUser, Guid planId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default);
    Task UpdatePlanDayAsync(UserEntity currentUser, string planDayId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises, CancellationToken cancellationToken = default);
    Task<PlanDayDetailsContext> GetPlanDayAsync(UserEntity currentUser, Guid planDayId, CancellationToken cancellationToken = default);
    Task<PlanDaysContext> GetPlanDaysAsync(UserEntity currentUser, Guid planId, CancellationToken cancellationToken = default);
    Task<List<PlanDayEntity>> GetPlanDaysTypesAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default);
    Task DeletePlanDayAsync(UserEntity currentUser, Guid planDayId, CancellationToken cancellationToken = default);
    Task<PlanDaysInfoContext> GetPlanDaysInfoAsync(UserEntity currentUser, Guid planId, CancellationToken cancellationToken = default);
}
