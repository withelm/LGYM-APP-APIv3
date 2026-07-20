using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.CheckIsUserHavePlan;

/// <summary>Compatibility-only seam for the legacy check-is-user-have-plan endpoint.</summary>
public interface ICheckIsUserHavePlanUseCase
{
    Task<Result<bool, AppError>> ExecuteAsync(CheckIsUserHavePlanQuery input, CancellationToken cancellationToken = default);
}
