using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface ITrainingRepository
{
    Task AddAsync(Training training, CancellationToken cancellationToken = default);
    Task<Training?> GetLastByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Training>> GetByUserIdAndDateAsync(Guid userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
    Task<List<DateTimeOffset>> GetDatesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Training>> GetByGymIdsAsync(List<Guid> gymIds, CancellationToken cancellationToken = default);
    Task<List<Training>> GetByPlanDayIdsAsync(List<Guid> planDayIds, CancellationToken cancellationToken = default);
}
