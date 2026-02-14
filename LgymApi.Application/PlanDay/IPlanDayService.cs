using LgymApi.Application.Features.PlanDay.Models;
using PlanDayEntity = LgymApi.Domain.Entities.PlanDay;

namespace LgymApi.Application.Features.PlanDay;

public interface IPlanDayService
{
    Task CreatePlanDayAsync(Guid planId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises);
    Task UpdatePlanDayAsync(string planDayId, string name, IReadOnlyCollection<PlanDayExerciseInput> exercises);
    Task<PlanDayDetailsContext> GetPlanDayAsync(Guid planDayId);
    Task<PlanDaysContext> GetPlanDaysAsync(Guid planId);
    Task<List<PlanDayEntity>> GetPlanDaysTypesAsync(Guid userId);
    Task DeletePlanDayAsync(Guid planDayId);
    Task<PlanDaysInfoContext> GetPlanDaysInfoAsync(Guid planId);
}
