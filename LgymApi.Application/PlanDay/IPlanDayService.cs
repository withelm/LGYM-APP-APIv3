using LgymApi.Application.Features.PlanDay.Models;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.PlanDay;

public interface IPlanDayService
{
    Task CreatePlanDayAsync(UserEntity currentUser, Guid planId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises);
    Task UpdatePlanDayAsync(UserEntity currentUser, string planDayId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises);
    Task<PlanDayDetailsContext> GetPlanDayAsync(UserEntity currentUser, Guid planDayId);
    Task<PlanDaysContext> GetPlanDaysAsync(UserEntity currentUser, Guid planId);
    Task<List<PlanDayEntity>> GetPlanDaysTypesAsync(UserEntity currentUser, Guid routeUserId);
    Task DeletePlanDayAsync(UserEntity currentUser, Guid planDayId);
    Task<PlanDaysInfoContext> GetPlanDaysInfoAsync(UserEntity currentUser, Guid planId);
}
