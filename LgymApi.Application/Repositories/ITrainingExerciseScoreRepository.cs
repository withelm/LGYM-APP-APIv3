using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface ITrainingExerciseScoreRepository
{
    Task AddRangeAsync(IEnumerable<TrainingExerciseScore> scores, CancellationToken cancellationToken = default);
    Task<List<TrainingExerciseScore>> GetByTrainingIdsAsync(List<Guid> trainingIds, CancellationToken cancellationToken = default);
}
