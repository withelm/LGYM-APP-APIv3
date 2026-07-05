using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
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

    public Task AddRangeAsync(IEnumerable<TrainingExerciseScore> items, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainingExerciseScores.AddRangeAsync(items, cancellationToken);
    }

    public Task<List<TrainingExerciseScore>> GetByTrainingIdsAsync(List<Id<Training>> trainingIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.TrainingExerciseScores
            .AsNoTracking()
            .Where(item => trainingIds.Contains(item.TrainingId))
            .OrderBy(item => item.TrainingId)
            .ThenBy(item => item.Order)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
    }
}
