using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IMainRecordRepository
{
    Task AddAsync(MainRecord record, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetByUserAndExerciseAsync(Id<User> userId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetByUserAndExercisesAsync(Id<User> userId, IReadOnlyCollection<Id<Exercise>> exerciseIds, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetBestByUserGroupedByExerciseAndUnitAsync(Id<User> userId, IReadOnlyCollection<Id<Exercise>>? exerciseIds = null, CancellationToken cancellationToken = default);
    Task<MainRecord?> FindByIdAsync(Id<MainRecord> id, CancellationToken cancellationToken = default);
    Task DeleteAsync(MainRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(MainRecord record, CancellationToken cancellationToken = default);
    Task<MainRecord?> GetLatestByUserAndExerciseAsync(Id<User> userId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default);
}
