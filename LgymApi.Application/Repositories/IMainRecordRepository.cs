using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IMainRecordRepository
{
    Task AddAsync(MainRecord record, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default);
    Task<List<MainRecord>> GetByUserAndExercisesAsync(Guid userId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken = default);
    Task<MainRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(MainRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(MainRecord record, CancellationToken cancellationToken = default);
    Task<MainRecord?> GetLatestByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default);
}
