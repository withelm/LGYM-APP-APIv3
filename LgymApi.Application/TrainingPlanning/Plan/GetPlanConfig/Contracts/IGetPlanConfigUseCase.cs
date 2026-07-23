using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.TrainingPlanning.Plan.Models;

namespace LgymApi.Application.TrainingPlanning.Plan.GetPlanConfig;

public interface IGetPlanConfigUseCase
{
    Task<Result<PlanReadModel, AppError>> ExecuteAsync(GetPlanConfigQuery input, CancellationToken cancellationToken = default);
}
