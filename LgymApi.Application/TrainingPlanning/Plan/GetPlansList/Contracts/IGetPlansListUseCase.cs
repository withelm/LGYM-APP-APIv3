using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.TrainingPlanning.Plan.Models;

namespace LgymApi.Application.TrainingPlanning.Plan.GetPlansList;

public interface IGetPlansListUseCase
{
    Task<Result<List<PlanReadModel>, AppError>> ExecuteAsync(GetPlansListQuery input, CancellationToken cancellationToken = default);
}
