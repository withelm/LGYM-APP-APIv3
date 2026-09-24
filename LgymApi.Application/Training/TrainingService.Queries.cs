using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.WorkoutProgress.TrainingExecution;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Training;

public sealed partial class TrainingService
{
    public Task<Result<WorkoutTrainingReadModel, AppError>> GetLastTrainingAsync(Id<LgymApi.Identity.Contracts.AccountReference> userId, CancellationToken cancellationToken = default)
        => _trainingHistoryReadService.GetLastTrainingAsync(userId, cancellationToken);

    public async Task<Result<TrainingsByDateWithTranslations, AppError>> GetTrainingByDateAsync(
        Id<LgymApi.Identity.Contracts.AccountReference> userId,
        DateTime createdAt,
        IReadOnlyList<string> cultures,
        CancellationToken cancellationToken = default)
    {
        var result = await _trainingHistoryReadService.GetTrainingByDateAsync(userId, createdAt, cancellationToken);
        if (result.IsFailure)
        {
            return Result<TrainingsByDateWithTranslations, AppError>.Failure(result.Error);
        }

        var translations = await _workoutProgress.GetExerciseDisplayNamesAsync(
            result.Value.SelectMany(training => training.Exercises).Select(exercise => exercise.ExerciseDetails.Id),
            cultures,
            cancellationToken);
        return Result<TrainingsByDateWithTranslations, AppError>.Success(new TrainingsByDateWithTranslations
        {
            Trainings = result.Value,
            Translations = translations
        });
    }

    public Task<Result<List<DateTime>, AppError>> GetTrainingDatesAsync(Id<LgymApi.Identity.Contracts.AccountReference> userId, CancellationToken cancellationToken = default)
        => _trainingHistoryReadService.GetTrainingDatesAsync(userId, cancellationToken);
}
