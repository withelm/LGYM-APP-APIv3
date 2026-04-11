using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using TrainingEntity = LgymApi.Domain.Entities.Training;

namespace LgymApi.Application.Features.Training;

public sealed partial class TrainingService
{
    public async Task<Result<TrainingEntity, AppError>> GetLastTrainingAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<TrainingEntity, AppError>.Failure(new InvalidTrainingDataError(Messages.InvalidId));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<TrainingEntity, AppError>.Failure(new TrainingNotFoundError(Messages.DidntFind));
        }

        var training = await _trainingRepository.GetLastByUserIdAsync(user.Id, cancellationToken);
        if (training == null)
        {
            return Result<TrainingEntity, AppError>.Failure(new TrainingNotFoundError(Messages.DidntFind));
        }

        return Result<TrainingEntity, AppError>.Success(training);
    }

    public async Task<Result<List<TrainingByDateDetails>, AppError>> GetTrainingByDateAsync(Id<LgymApi.Domain.Entities.User> userId, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<List<TrainingByDateDetails>, AppError>.Failure(new InvalidTrainingDataError(Messages.InvalidId));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<List<TrainingByDateDetails>, AppError>.Failure(new TrainingNotFoundError(Messages.DidntFind));
        }

        var startOfDay = new DateTimeOffset(DateTime.SpecifyKind(createdAt.Date, DateTimeKind.Utc));
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        var trainings = await _trainingRepository.GetByUserIdAndDateAsync(user.Id, startOfDay, endOfDay, cancellationToken);
        if (trainings.Count == 0)
        {
            return Result<List<TrainingByDateDetails>, AppError>.Failure(new TrainingNotFoundError(Messages.DidntFind));
        }

        var trainingIds = trainings.Select(t => t.Id).ToList();
        var trainingScoreRefs = await _trainingExerciseScoreRepository.GetByTrainingIdsAsync(trainingIds, cancellationToken);

        var scoreIds = trainingScoreRefs.Select(t => t.ExerciseScoreId).Distinct().ToList();
        var scores = await _exerciseScoreRepository.GetByIdsAsync(scoreIds, cancellationToken);
        var scoreMap = scores.ToDictionary(s => s.Id, s => s);

        var result = new List<TrainingByDateDetails>();
        foreach (var training in trainings)
        {
            var exerciseRefs = trainingScoreRefs.Where(t => t.TrainingId == training.Id).ToList();
            var grouped = new Dictionary<Id<LgymApi.Domain.Entities.Exercise>, EnrichedExercise>();
            var exerciseOrderMap = new Dictionary<Id<LgymApi.Domain.Entities.Exercise>, int>();

            foreach (var reference in exerciseRefs)
            {
                if (!scoreMap.TryGetValue(reference.ExerciseScoreId, out var score) || score.Exercise == null)
                {
                    continue;
                }

                if (!grouped.TryGetValue(score.ExerciseId, out var group))
                {
                    group = new EnrichedExercise
                    {
                        ExerciseScoreId = reference.ExerciseScoreId,
                        ExerciseDetails = score.Exercise,
                        ScoresDetails = new List<ExerciseScore>()
                    };
                    grouped[score.ExerciseId] = group;
                    exerciseOrderMap[score.ExerciseId] = reference.Order;
                }
                else
                {
                    exerciseOrderMap[score.ExerciseId] = Math.Min(exerciseOrderMap[score.ExerciseId], reference.Order);
                }

                group.ScoresDetails.Add(score);
            }

            var orderedExercises = grouped.Values
                .OrderBy(ex => exerciseOrderMap[ex.ExerciseDetails.Id])
                .Select(ex => new EnrichedExercise
                {
                    ExerciseScoreId = ex.ExerciseScoreId,
                    ExerciseDetails = ex.ExerciseDetails,
                    ScoresDetails = ex.ScoresDetails.OrderBy(s => s.Series).ToList()
                })
                .ToList();

            result.Add(new TrainingByDateDetails
            {
                Id = training.Id,
                TypePlanDayId = training.TypePlanDayId,
                CreatedAt = training.CreatedAt.UtcDateTime,
                PlanDay = training.PlanDay,
                Gym = training.Gym?.Name,
                Exercises = orderedExercises
            });
        }

        return Result<List<TrainingByDateDetails>, AppError>.Success(result);
    }

    public async Task<Result<List<DateTime>, AppError>> GetTrainingDatesAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return Result<List<DateTime>, AppError>.Failure(new InvalidTrainingDataError(Messages.InvalidId));
        }

        var trainings = await _trainingRepository.GetDatesByUserIdAsync(userId, cancellationToken);
        if (trainings.Count == 0)
        {
            return Result<List<DateTime>, AppError>.Failure(new TrainingNotFoundError(Messages.DidntFind));
        }

        return Result<List<DateTime>, AppError>.Success(trainings.Select(t => t.UtcDateTime).ToList());
    }
}
