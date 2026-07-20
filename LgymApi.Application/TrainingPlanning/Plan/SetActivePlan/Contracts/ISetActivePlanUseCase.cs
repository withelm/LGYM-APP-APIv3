using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.SetActivePlan;

public interface ISetActivePlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(SetActivePlanCommand input, CancellationToken cancellationToken = default);
}
