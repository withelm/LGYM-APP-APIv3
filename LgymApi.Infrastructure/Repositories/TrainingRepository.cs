using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
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

    public Task AddAsync(Training training, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings.AddAsync(training, cancellationToken).AsTask();
    }

    public Task<Training?> GetByIdAsync(Id<Training> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .AsNoTracking()
            .Include(t => t.PlanDay)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<Training?> GetLastByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .AsNoTracking()
            .Include(t => t.PlanDay)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
    }

    public Task<List<Training>> GetByUserIdAndDateAsync(Id<User> userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .AsNoTracking()
            .Include(t => t.PlanDay)
            .Include(t => t.Gym)
            .Where(t => t.UserId == userId && t.CreatedAt >= start && t.CreatedAt <= end)
            .ToListAsync(cancellationToken);
    }

    public Task<List<DateTimeOffset>> GetDatesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Training>> GetByGymIdsAsync(List<Id<Gym>> gymIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .AsNoTracking()
            .Include(t => t.PlanDay)
            .Where(t => gymIds.Contains(t.GymId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<Training>> GetByPlanDayIdsAsync(List<Id<PlanDay>> planDayIds, CancellationToken cancellationToken = default)
    {
        return _dbContext.Trainings
            .AsNoTracking()
            .Where(t => planDayIds.Contains(t.TypePlanDayId))
            .ToListAsync(cancellationToken);
    }
}
