using LgymApi.Application.Features.Exercise.Models;

namespace LgymApi.Application.Features.Exercise;

public interface IExerciseService
{
    Task AddExerciseAsync(string name, string bodyPart, string? description, string? image);
    Task AddUserExerciseAsync(Guid userId, string name, string bodyPart, string? description, string? image);
    Task DeleteExerciseAsync(Guid userId, Guid exerciseId);
    Task UpdateExerciseAsync(string exerciseId, string? name, string? bodyPart, string? description, string? image);
    Task AddGlobalTranslationAsync(LgymApi.Domain.Entities.User currentUser, Guid routeUserId, string exerciseId, string? culture, string? name);
    Task<ExercisesWithTranslations> GetAllExercisesAsync(Guid userId, IReadOnlyList<string> cultures);
    Task<ExercisesWithTranslations> GetAllUserExercisesAsync(Guid userId, IReadOnlyList<string> cultures);
    Task<ExercisesWithTranslations> GetAllGlobalExercisesAsync(IReadOnlyList<string> cultures);
    Task<ExercisesWithTranslations> GetExerciseByBodyPartAsync(Guid userId, string bodyPart, IReadOnlyList<string> cultures);
    Task<ExerciseWithTranslations> GetExerciseAsync(Guid exerciseId, IReadOnlyList<string> cultures);
    Task<LastExerciseScoresResult> GetLastExerciseScoresAsync(Guid routeUserId, Guid currentUserId, Guid exerciseId, int series, Guid? gymId, string exerciseName);
    Task<List<ExerciseTrainingHistoryItem>> GetExerciseScoresFromTrainingByExerciseAsync(Guid currentUserId, Guid exerciseId);
}
