using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.TrainingPlanning.Plan.Models;

namespace LgymApi.Application.TrainingPlanning.Plan.GetPlansList;

internal sealed class GetPlansListUseCase : IGetPlansListUseCase
{
    private readonly Func<GetPlansListQuery, CancellationToken, Task<Result<List<PlanReadModel>, AppError>>> _executeAsync;

    public GetPlansListUseCase(Func<GetPlansListQuery, CancellationToken, Task<Result<List<PlanReadModel>, AppError>>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        _executeAsync = executeAsync;
    }

    public Task<Result<List<PlanReadModel>, AppError>> ExecuteAsync(GetPlansListQuery input, CancellationToken cancellationToken = default)
        => _executeAsync(input, cancellationToken);
}
