using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IPlanRepository
{
    Task<Plan?> FindByIdAsync(Id<Plan> id, CancellationToken cancellationToken = default);
    Task<Plan?> FindActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task<Plan?> FindLastActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task<List<Plan>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task AddAsync(Plan plan, CancellationToken cancellationToken = default);
    Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default);
    Task SetActivePlanAsync(Id<User> userId, Id<Plan> planId, CancellationToken cancellationToken = default);
    Task ClearActivePlansAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task<Plan> CopyPlanByShareCodeAsync(string shareCode, Id<User> userId, CancellationToken cancellationToken = default);
    Task<string> GenerateShareCodeAsync(Id<Plan> planId, Id<User> userId, CancellationToken cancellationToken = default);
}

