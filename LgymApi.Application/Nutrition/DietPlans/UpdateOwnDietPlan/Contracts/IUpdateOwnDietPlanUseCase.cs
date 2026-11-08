using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Nutrition.DietPlans.Models;

namespace LgymApi.Application.Nutrition.DietPlans.UpdateOwnDietPlan.Contracts;

public interface IUpdateOwnDietPlanUseCase
{
    Task<Result<DietPlanReadModel, AppError>> ExecuteAsync(
        UpdateOwnDietPlanCommand command,
        CancellationToken cancellationToken = default);
}
