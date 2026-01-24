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

    public async Task AddAsync(MainRecord record, CancellationToken cancellationToken = default)
    {
        await _dbContext.MainRecords.AddAsync(record, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<List<MainRecord>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<MainRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task DeleteAsync(MainRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.MainRecords.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MainRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.MainRecords.Update(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<MainRecord?> GetLatestByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
    {
        return _dbContext.MainRecords
            .Where(r => r.UserId == userId && r.ExerciseId == exerciseId)
            .OrderByDescending(r => r.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
