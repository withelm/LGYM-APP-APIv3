using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class TrainingExerciseScoreRepository : ITrainingExerciseScoreRepository
{
    private readonly AppDbContext _dbContext;

    public TrainingExerciseScoreRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddRangeAsync(IEnumerable<TrainingExerciseScore> scores, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainingExerciseScores.AddRangeAsync(scores, cancellationToken);
    }

    public Task<List<TrainingExerciseScore>> GetByTrainingIdsAsync(List<Guid> trainingIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainingExerciseScores
            .AsNoTracking()
            .Where(t => trainingIds.Contains(t.TrainingId))
            .ToListAsync(cancellationToken);
    }
}
