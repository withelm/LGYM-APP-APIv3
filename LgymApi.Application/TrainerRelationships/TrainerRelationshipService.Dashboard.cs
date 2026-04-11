using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Domain.ValueObjects;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;
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

        var result = await _trainingService.GetTrainingDatesAsync(traineeId, cancellationToken);
 
        if (result.IsFailure)
        {
            return Result<List<DateTime>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<DateTime>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<TrainingByDateDetails>, AppError>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<TrainingByDateDetails>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _trainingService.GetTrainingByDateAsync(traineeId, createdAt, cancellationToken);

        if (result.IsFailure)
        {
            return Result<List<TrainingByDateDetails>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<TrainingByDateDetails>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<ExerciseScoresChartData>, AppError>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<ExerciseEntity> exerciseId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _exerciseScoresService.GetExerciseScoresChartDataAsync(traineeId, exerciseId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<ExerciseScoresChartData>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<EloRegistryChartEntry>, AppError>> GetTraineeEloChartAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _eloRegistryService.GetChartAsync(traineeId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<EloRegistryChartEntry>, AppError>.Success(result.Value);
    }

    public async Task<Result<List<MainRecordEntity>, AppError>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default)
    {
        var ensureResult = await EnsureTrainerOwnsTraineeAsync(currentTrainer, traineeId, cancellationToken);
        if (ensureResult.IsFailure)
        {
            return Result<List<MainRecordEntity>, AppError>.Failure(ensureResult.Error);
        }

        var result = await _mainRecordsService.GetMainRecordsHistoryAsync(traineeId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<List<MainRecordEntity>, AppError>.Failure(new TrainerRelationshipNotFoundError(result.Error.Message));
        }

        return Result<List<MainRecordEntity>, AppError>.Success(result.Value);
    }
}
