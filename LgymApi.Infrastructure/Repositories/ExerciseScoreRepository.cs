using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class ExerciseScoreRepository : IExerciseScoreRepository
{
    private readonly AppDbContext _dbContext;

    public ExerciseScoreRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddRangeAsync(IEnumerable<ExerciseScore> scores, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExerciseScores.AddRangeAsync(scores, cancellationToken);
    }

    public Task<List<ExerciseScore>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExerciseScores
            .AsNoTracking()
            .Include(s => s.Exercise)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<List<ExerciseScore>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExerciseScores
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ExerciseId == exerciseId)
            .Include(s => s.Exercise)
            .Include(s => s.Training)
                .ThenInclude(t => t!.Gym)
            .Include(s => s.Training)
                .ThenInclude(t => t!.PlanDay)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ExerciseScore>> GetByUserAndExerciseAndGymAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ExerciseScores
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ExerciseId == exerciseId)
            .Include(s => s.Training)
                .ThenInclude(t => t!.Gym)
            .AsQueryable();

        if (gymId.HasValue)
        {
            query = query.Where(s => s.Training != null && s.Training.GymId == gymId.Value);
        }

        return query.OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<List<ExerciseScore>> GetByUserAndExercisesAsync(Guid userId, List<Guid> exerciseIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExerciseScores
            .AsNoTracking()
            .Where(s => s.UserId == userId && exerciseIds.Contains(s.ExerciseId))
            .Include(s => s.Training)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ExerciseScore>> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ExerciseScores
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ExerciseId == exerciseId)
            .AsQueryable();

        if (gymId.HasValue)
        {
            query = query.Where(s => s.Training != null && s.Training.GymId == gymId.Value);
        }

        return query
            .GroupBy(s => s.Series)
            .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
            .ToListAsync(cancellationToken);
    }

    public Task<ExerciseScore?> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, int series, Guid? gymId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ExerciseScores
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ExerciseId == exerciseId && s.Series == series)
            .Include(s => s.Training)
                .ThenInclude(t => t!.Gym)
            .AsQueryable();

        if (gymId.HasValue)
        {
            query = query.Where(s => s.Training != null && s.Training.GymId == gymId.Value);
        }

        return query.OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ExerciseScore?> GetBestScoreAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExerciseScores
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ExerciseId == exerciseId)
            .OrderByDescending(s => s.Weight)
            .ThenByDescending(s => s.Reps)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
