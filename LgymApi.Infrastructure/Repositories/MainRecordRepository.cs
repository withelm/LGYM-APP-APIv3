using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class MainRecordRepository : IMainRecordRepository
{
    private readonly AppDbContext _dbContext;

    public MainRecordRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(MainRecord record, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords.AddAsync(record, cancellationToken).AsTask();
    }

    public Task<List<MainRecord>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<MainRecord>> GetByUserAndExerciseAsync(Id<User> userId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.ExerciseId == exerciseId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<MainRecord>> GetByUserAndExercisesAsync(Id<User> userId, IReadOnlyCollection<Id<Exercise>> exerciseIds, CancellationToken cancellationToken = default)
    {
        if (exerciseIds.Count == 0)
        {
            return Task.FromResult(new List<MainRecord>());
        }

        return _dbContext.MainRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId && exerciseIds.Contains(r.ExerciseId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<MainRecord>> GetBestByUserGroupedByExerciseAndUnitAsync(Id<User> userId, IReadOnlyCollection<Id<Exercise>>? exerciseIds = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MainRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId);

        if (exerciseIds is { Count: > 0 })
        {
            query = query.Where(r => exerciseIds.Contains(r.ExerciseId));
        }

        return query
            .GroupBy(r => new { r.ExerciseId, r.Unit })
            .Select(g => g
                .OrderByDescending(r => r.WeightValue)
                .ThenByDescending(r => r.Date)
                .First())
            .ToListAsync(cancellationToken);
    }

    public Task<MainRecord?> FindByIdAsync(Id<MainRecord> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task DeleteAsync(MainRecord record, CancellationToken cancellationToken = default)
    {
        record.IsDeleted = true;
        _dbContext.MainRecords.Update(record);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MainRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.MainRecords.Update(record);
        return Task.CompletedTask;
    }

    public Task<MainRecord?> GetLatestByUserAndExerciseAsync(Id<User> userId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.ExerciseId == exerciseId)
            .OrderByDescending(r => r.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
