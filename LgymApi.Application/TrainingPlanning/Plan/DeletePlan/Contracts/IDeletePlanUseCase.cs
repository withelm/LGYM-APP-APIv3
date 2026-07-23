using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.DeletePlan;

public interface IDeletePlanUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(DeletePlanCommand input, CancellationToken cancellationToken = default);
}
