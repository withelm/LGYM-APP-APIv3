using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IExerciseScoreRepository
{
    Task AddRangeAsync(IEnumerable<ExerciseScore> scores, CancellationToken cancellationToken = default);
    Task<List<ExerciseScore>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default);
    Task<List<ExerciseScore>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default);
    Task<List<ExerciseScore>> GetByUserAndExerciseAndGymAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default);
    Task<List<ExerciseScore>> GetByUserAndExercisesAsync(Guid userId, List<Guid> exerciseIds, CancellationToken cancellationToken = default);
    Task<List<ExerciseScore>> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default);
    Task<ExerciseScore?> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, int series, Guid? gymId, CancellationToken cancellationToken = default);
    Task<ExerciseScore?> GetBestScoreAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default);
}
