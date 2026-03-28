using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface ITrainingRepository
{
    Task AddAsync(Training training, CancellationToken cancellationToken = default);
    Task<Training?> GetByIdAsync(Id<Training> id, CancellationToken cancellationToken = default);
    Task<Training?> GetLastByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task<List<Training>> GetByUserIdAndDateAsync(Id<User> userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
    Task<List<DateTimeOffset>> GetDatesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task<List<Training>> GetByGymIdsAsync(List<Id<Gym>> gymIds, CancellationToken cancellationToken = default);
    Task<List<Training>> GetByPlanDayIdsAsync(List<Id<PlanDay>> planDayIds, CancellationToken cancellationToken = default);
}
