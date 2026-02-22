using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
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

    public Task<List<MainRecord>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<MainRecord>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.ExerciseId == exerciseId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<MainRecord>> GetByUserAndExercisesAsync(Guid userId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken = default)
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

    public Task<List<MainRecord>> GetBestByUserGroupedByExerciseAndUnitAsync(Guid userId, IReadOnlyCollection<Guid>? exerciseIds = null, CancellationToken cancellationToken = default)
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
                .OrderByDescending(r => r.Weight)
                .ThenByDescending(r => r.Date)
                .First())
            .ToListAsync(cancellationToken);
    }

    public Task<MainRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
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

    public Task<MainRecord?> GetLatestByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.ExerciseId == exerciseId)
            .OrderByDescending(r => r.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
