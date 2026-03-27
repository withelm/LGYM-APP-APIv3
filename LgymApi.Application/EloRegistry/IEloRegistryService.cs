using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.EloRegistry;

public interface IEloRegistryService
{
    Task<List<EloRegistryChartEntry>> GetChartAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default);
}
