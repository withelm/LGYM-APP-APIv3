using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.UpdatePlan;

public interface IUpdatePlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(UpdatePlanCommand input, CancellationToken cancellationToken = default);
}
