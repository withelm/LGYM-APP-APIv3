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

    public async Task<Dictionary<Guid, string>> GetTranslationsAsync(IEnumerable<Guid> exerciseIds, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
    {
        var ids = exerciseIds.Distinct().ToList();
        if (ids.Count == 0 || cultures.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var cultureIndex = cultures
            .Select((culture, index) => (culture, index))
            .ToDictionary(x => x.culture, x => x.index, StringComparer.OrdinalIgnoreCase);

        var translations = await _dbContext.ExerciseTranslations
            .Where(t => ids.Contains(t.ExerciseId) && cultures.Contains(t.Culture))
            .Select(t => new { t.ExerciseId, t.Culture, t.Name })
            .ToListAsync(cancellationToken);

        return translations
            .OrderBy(t => cultureIndex.TryGetValue(t.Culture, out var index) ? index : int.MaxValue)
            .GroupBy(t => t.ExerciseId)
            .ToDictionary(g => g.Key, g => g.First().Name);
    }

    public async Task UpsertTranslationAsync(Guid exerciseId, string culture, string name, CancellationToken cancellationToken = default)
    {
        var translation = await _dbContext.ExerciseTranslations
            .FirstOrDefaultAsync(t => t.ExerciseId == exerciseId && t.Culture == culture, cancellationToken);

        if (translation == null)
        {
            translation = new ExerciseTranslation
            {
                Id = Guid.NewGuid(),
                ExerciseId = exerciseId,
                Culture = culture,
                Name = name
            };

            await _dbContext.ExerciseTranslations.AddAsync(translation, cancellationToken);
        }
        else
        {
            translation.Name = name;
            _dbContext.ExerciseTranslations.Update(translation);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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
