using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IExerciseRepository
{
    Task<Exercise?> FindByIdAsync(Id<Exercise> id, CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetAllForUserAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetAllGlobalAsync(CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetUserExercisesAsync(Id<User> userId, CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetByBodyPartAsync(Id<User> userId, BodyParts bodyPart, CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetByIdsAsync(List<Id<Exercise>> ids, CancellationToken cancellationToken = default);
    Task<Dictionary<Id<Exercise>, string>> GetTranslationsAsync(IEnumerable<Id<Exercise>> exerciseIds, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task UpsertTranslationAsync(Id<Exercise> exerciseId, string culture, string name, CancellationToken cancellationToken = default);
    Task AddAsync(Exercise exercise, CancellationToken cancellationToken = default);
    Task UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default);
}
