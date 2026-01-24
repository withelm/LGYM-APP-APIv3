using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IExerciseRepository
{
    Task<Exercise?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetAllGlobalAsync(CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetUserExercisesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetByBodyPartAsync(Guid userId, string bodyPart, CancellationToken cancellationToken = default);
    Task<List<Exercise>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default);
    Task AddAsync(Exercise exercise, CancellationToken cancellationToken = default);
    Task UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default);
}
