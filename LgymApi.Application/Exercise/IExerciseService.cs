using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Exercise;

public interface IExerciseService
{
    Task AddExerciseAsync(string name, BodyParts bodyPart, string? description, string? image, CancellationToken cancellationToken = default);
    Task AddUserExerciseAsync(AddUserExerciseInput input, CancellationToken cancellationToken = default);
    Task DeleteExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default);
    Task UpdateExerciseAsync(UpdateExerciseInput input, CancellationToken cancellationToken = default);
    Task AddGlobalTranslationAsync(LgymApi.Domain.Entities.User currentUser, AddGlobalTranslationInput input, CancellationToken cancellationToken = default);
    Task<ExercisesWithTranslations> GetAllExercisesAsync(Guid userId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<ExercisesWithTranslations> GetAllUserExercisesAsync(Guid userId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<ExercisesWithTranslations> GetAllGlobalExercisesAsync(IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<ExercisesWithTranslations> GetExerciseByBodyPartAsync(Guid userId, BodyParts bodyPart, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<ExerciseWithTranslations> GetExerciseAsync(Guid exerciseId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<LastExerciseScoresResult> GetLastExerciseScoresAsync(GetLastExerciseScoresInput input, CancellationToken cancellationToken = default);
    Task<List<ExerciseTrainingHistoryItem>> GetExerciseScoresFromTrainingByExerciseAsync(Guid currentUserId, Guid exerciseId, CancellationToken cancellationToken = default);
}
