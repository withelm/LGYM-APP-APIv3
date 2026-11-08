using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.DietPlans.UpdateOwnDietPlan.Contracts;
using LgymApi.Application.Nutrition.DietPlans.UpdateTraineePlan;
using LgymApi.Application.Nutrition.DietPlans.UpdateTraineePlan.Contracts;

namespace LgymApi.Application.Nutrition.DietPlans.UpdateOwnDietPlan;

internal sealed class UpdateOwnDietPlanUseCase : IUpdateOwnDietPlanUseCase
{
    private readonly IUpdateTraineeDietPlanUseCase _update;

    public UpdateOwnDietPlanUseCase(IUpdateTraineeDietPlanUseCase update)
    {
        _update = update;
    }

    public Task<Result<DietPlanReadModel, AppError>> ExecuteAsync(
        UpdateOwnDietPlanCommand command,
        CancellationToken cancellationToken = default)
        => _update.ExecuteAsync(
            new UpdateTraineeDietPlanCommand(command.Actor, command.TraineeId, command.TraineeId, command.DietPlanId, command.Data),
            cancellationToken);
}
