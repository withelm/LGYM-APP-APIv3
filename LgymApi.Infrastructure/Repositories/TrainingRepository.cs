using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class TrainingRepository : ITrainingRepository
{
    private readonly AppDbContext _dbContext;

    public TrainingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Training training, CancellationToken cancellationToken = default)
    {
        await _dbContext.Trainings.AddAsync(training, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Training?> GetLastByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .Include(t => t.PlanDay)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
    }

    public Task<List<Training>> GetByUserIdAndDateAsync(Guid userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .Include(t => t.PlanDay)
            .Include(t => t.Gym)
            .Where(t => t.UserId == userId && t.CreatedAt >= start && t.CreatedAt <= end)
            .ToListAsync(cancellationToken);
    }

    public Task<List<DateTimeOffset>> GetDatesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Training>> GetByGymIdsAsync(List<Guid> gymIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .Include(t => t.PlanDay)
            .Where(t => gymIds.Contains(t.GymId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<Training>> GetByPlanDayIdsAsync(List<Guid> planDayIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .Where(t => planDayIds.Contains(t.TypePlanDayId))
            .ToListAsync(cancellationToken);
    }
}
