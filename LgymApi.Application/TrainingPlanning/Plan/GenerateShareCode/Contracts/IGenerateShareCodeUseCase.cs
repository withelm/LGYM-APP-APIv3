using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.TrainingPlanning.Plan.GenerateShareCode;

public interface IGenerateShareCodeUseCase
{
    Task<Result<string, AppError>> ExecuteAsync(GenerateShareCodeCommand input, CancellationToken cancellationToken = default);
}
