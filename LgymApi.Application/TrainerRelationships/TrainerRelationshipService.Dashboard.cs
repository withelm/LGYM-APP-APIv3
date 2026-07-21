using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.WorkoutProgress.Dashboard.Models;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public sealed partial class TrainerRelationshipService
{
    public async Task<Result<TrainerDashboardTraineeListResult, AppError>> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default)
    {
        var ensureTrainerResult = await EnsureTrainerAsync(currentTrainer, cancellationToken);
        if (ensureTrainerResult.IsFailure)
        {
            return Result<TrainerDashboardTraineeListResult, AppError>.Failure(ensureTrainerResult.Error);
        }

        return Result<TrainerDashboardTraineeListResult, AppError>.Success(
            await _trainerRelationshipRepository.GetDashboardTraineesAsync(currentTrainer.Id, query, cancellationToken));
    }

    public async Task<Result<List<DateTime>, AppError>> GetTraineeTrainingDatesAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<DateTime>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _workoutProgressDashboardReadService.GetTrainingDatesAsync(traineeId, cancellationToken);
 
        if (result.IsFailure)
        {
            return Result<List<DateTime>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<DateTime>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<WorkoutProgressDashboardTrainingReadModel>, AppError>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<WorkoutProgressDashboardTrainingReadModel>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _workoutProgressDashboardReadService.GetTrainingByDateAsync(traineeId, createdAt, cancellationToken);

        if (result.IsFailure)
        {
            return Result<List<WorkoutProgressDashboardTrainingReadModel>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<WorkoutProgressDashboardTrainingReadModel>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<ExerciseScoreChartPoint>, AppError>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, string exerciseId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<ExerciseScoreChartPoint>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _workoutProgressDashboardReadService.GetExerciseScoreChartAsync(traineeId, exerciseId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<List<ExerciseScoreChartPoint>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<ExerciseScoreChartPoint>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<EloChartPoint>, AppError>> GetTraineeEloChartAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<EloChartPoint>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _workoutProgressDashboardReadService.GetEloChartAsync(traineeId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<List<EloChartPoint>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<EloChartPoint>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<MainRecordReadModel>, AppError>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<MainRecordReadModel>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _workoutProgressDashboardReadService.GetMainRecordHistoryAsync(traineeId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<List<MainRecordReadModel>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<MainRecordReadModel>, AppError>.Success(result.Value);
    }
}
