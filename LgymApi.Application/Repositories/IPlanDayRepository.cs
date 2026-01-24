using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IPlanDayRepository
{
    Task<PlanDay?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<PlanDay>> GetByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default);
    Task AddAsync(PlanDay planDay, CancellationToken cancellationToken = default);
    Task UpdateAsync(PlanDay planDay, CancellationToken cancellationToken = default);
    Task MarkDeletedAsync(Guid planDayId, CancellationToken cancellationToken = default);
    Task<bool> AnyByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default);
}
