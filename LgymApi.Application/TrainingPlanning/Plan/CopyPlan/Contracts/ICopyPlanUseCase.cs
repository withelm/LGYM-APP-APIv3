using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.TrainingPlanning.Plan.Models;

namespace LgymApi.Application.TrainingPlanning.Plan.CopyPlan;

public interface ICopyPlanUseCase
{
    Task<Result<PlanReadModel, AppError>> ExecuteAsync(CopyPlanCommand input, CancellationToken cancellationToken = default);
}
