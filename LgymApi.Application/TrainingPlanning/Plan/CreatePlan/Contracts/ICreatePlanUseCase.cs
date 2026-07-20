using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.CreatePlan;

public interface ICreatePlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(CreatePlanCommand input, CancellationToken cancellationToken = default);
}
