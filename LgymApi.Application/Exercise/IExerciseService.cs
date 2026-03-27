using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Exercise;

public interface IExerciseService
{
    Task AddExerciseAsync(string name, BodyParts bodyPart, string? description, string? image, CancellationToken cancellationToken = default);
    Task AddUserExerciseAsync(AddUserExerciseInput input, CancellationToken cancellationToken = default);
    Task DeleteExerciseAsync(Id<UserEntity> userId, Id<ExerciseEntity> exerciseId, CancellationToken cancellationToken = default);
    Task UpdateExerciseAsync(UpdateExerciseInput input, CancellationToken cancellationToken = default);
    Task AddGlobalTranslationAsync(LgymApi.Domain.Entities.User currentUser, AddGlobalTranslationInput input, CancellationToken cancellationToken = default);
    Task<ExercisesWithTranslations> GetAllExercisesAsync(Id<UserEntity> userId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<ExercisesWithTranslations> GetAllUserExercisesAsync(Id<UserEntity> userId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<ExercisesWithTranslations> GetAllGlobalExercisesAsync(IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<ExercisesWithTranslations> GetExerciseByBodyPartAsync(Id<UserEntity> userId, BodyParts bodyPart, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<ExerciseWithTranslations> GetExerciseAsync(Id<ExerciseEntity> exerciseId, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default);
    Task<LastExerciseScoresResult> GetLastExerciseScoresAsync(GetLastExerciseScoresInput input, CancellationToken cancellationToken = default);
    Task<List<ExerciseTrainingHistoryItem>> GetExerciseScoresFromTrainingByExerciseAsync(Id<UserEntity> currentUserId, Id<ExerciseEntity> exerciseId, CancellationToken cancellationToken = default);
}
