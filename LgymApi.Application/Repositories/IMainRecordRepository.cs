using LgymApi.Domain.Entities;
using LgymApi.Application.Features.MainRecords.Strategies;

namespace LgymApi.Application.Repositories;

public interface IMainRecordRepository
{
    Task AddAsync(MainRecord record, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetByUserAndExercisesAsync(Guid userId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetBestByUserGroupedByExerciseAndUnitAsync(Guid userId, IRecordComparisonStrategyResolver strategyResolver, IReadOnlyCollection<Guid>? exerciseIds = null, CancellationToken cancellationToken = default);
    Task<MainRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(MainRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(MainRecord record, CancellationToken cancellationToken = default);
    Task<MainRecord?> GetLatestByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default);
}
