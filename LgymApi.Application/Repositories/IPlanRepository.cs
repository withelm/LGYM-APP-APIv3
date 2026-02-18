using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IPlanRepository
{
    Task<Plan?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Plan?> FindActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Plan?> FindLastActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Plan>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Plan plan, CancellationToken cancellationToken = default);
    Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default);
    Task SetActivePlanAsync(Guid userId, Guid planId, CancellationToken cancellationToken = default);
    Task<Plan> CopyPlanByShareCodeAsync(string shareCode, Guid userId, CancellationToken cancellationToken = default);
    Task<string> GenerateShareCodeAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default);
}

