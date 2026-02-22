using LgymApi.Application.Features.EloRegistry.Models;

namespace LgymApi.Application.Features.EloRegistry;

public interface IEloRegistryService
{
    Task<List<EloRegistryChartEntry>> GetChartAsync(Guid userId, CancellationToken cancellationToken = default);
}
