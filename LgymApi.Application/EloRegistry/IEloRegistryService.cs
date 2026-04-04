using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.EloRegistry;

public interface IEloRegistryService
{
    Task<Result<List<EloRegistryChartEntry>, AppError>> GetChartAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default);
}
