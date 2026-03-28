using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface ITrainingExerciseScoreRepository
{
    Task AddRangeAsync(IEnumerable<TrainingExerciseScore> scores, CancellationToken cancellationToken = default);
    Task<List<TrainingExerciseScore>> GetByTrainingIdsAsync(List<Id<Training>> trainingIds, CancellationToken cancellationToken = default);
}
