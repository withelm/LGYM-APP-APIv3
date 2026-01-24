using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class ExerciseRepository : IExerciseRepository
{
    private readonly AppDbContext _dbContext;

    public ExerciseRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Exercise?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Exercises.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public Task<List<Exercise>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Exercises
            .Where(e => e.UserId == userId || e.UserId == null)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Exercise>> GetAllGlobalAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Exercises
            .Where(e => e.UserId == null && !e.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Exercise>> GetUserExercisesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Exercises
            .Where(e => e.UserId == userId && !e.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Exercise>> GetByBodyPartAsync(Guid userId, string bodyPart, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<BodyParts>(bodyPart, out var parsed))
        {
            return Task.FromResult(new List<Exercise>());
        }

        return _dbContext.Exercises
            .Where(e => e.BodyPart == parsed && !e.IsDeleted && (e.UserId == userId || e.UserId == null))
            .ToListAsync(cancellationToken);
    }

    public Task<List<Exercise>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default)
    {
        return _dbContext.Exercises
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Exercise exercise, CancellationToken cancellationToken = default)
    {
        await _dbContext.Exercises.AddAsync(exercise, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default)
    {
        _dbContext.Exercises.Update(exercise);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
