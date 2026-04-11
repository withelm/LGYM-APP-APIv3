using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Training.Models;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using TrainingEntity = LgymApi.Domain.Entities.Training;

namespace LgymApi.Application.Features.Training;

public sealed partial class TrainingService
{
    public async Task<Result<TrainingSummaryResult, AppError>> AddTrainingAsync(
        Id<LgymApi.Domain.Entities.User> userId,
        AddTrainingInput input,
        CancellationToken cancellationToken = default)
    {
        var (gymId, planDayId, createdAt, exercises) = input;
        if (userId.IsEmpty || gymId.IsEmpty || planDayId.IsEmpty)
        {
            return Result<TrainingSummaryResult, AppError>.Failure(new InvalidTrainingDataError(Messages.InvalidId));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)userId, cancellationToken);
        if (user == null)
        {
            return Result<TrainingSummaryResult, AppError>.Failure(new TrainingNotFoundError(Messages.DidntFind));
        }

        var gym = await _gymRepository.FindByIdAsync(gymId, cancellationToken);
        if (gym == null)
        {
            return Result<TrainingSummaryResult, AppError>.Failure(new TrainingNotFoundError(Messages.DidntFind));
        }

        var uniqueExerciseIds = exercises
            .Select(e => e.ExerciseId)
            .Where(id => !id.IsEmpty)
            .Distinct()
            .ToList();

        var exerciseDetails = await _exerciseRepository.GetByIdsAsync(uniqueExerciseIds, cancellationToken);
        var exerciseDetailsMap = exerciseDetails.ToDictionary(e => e.Id, e => e.Name);

        var previousScoresMap = await FetchPreviousScores(user.Id, gym.Id, uniqueExerciseIds, cancellationToken);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var createdAtUtc = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
            var training = new TrainingEntity
            {
                Id = Id<LgymApi.Domain.Entities.Training>.New(),
                UserId = user.Id,
                TypePlanDayId = planDayId,
                CreatedAt = new DateTimeOffset(createdAtUtc),
                GymId = gym.Id
            };

            await _trainingRepository.AddAsync(training, cancellationToken);

            var savedScoreIds = new List<Id<ExerciseScore>>();
            var totalElo = 0;
            var scoresToAdd = new List<ExerciseScore>();
            var index = 0;
            foreach (var exercise in exercises)
            {
                if (exercise.ExerciseId.IsEmpty)
                {
                    continue;
                }

                var exerciseId = exercise.ExerciseId;

                if (exercise.Unit == WeightUnits.Unknown)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return Result<TrainingSummaryResult, AppError>.Failure(new InvalidTrainingDataError(Messages.FieldRequired));
                }

                var scoreEntity = new ExerciseScore
                {
                    Id = Id<ExerciseScore>.New(),
                    ExerciseId = exerciseId,
                    UserId = user.Id,
                    Reps = exercise.Reps,
                    Series = exercise.Series,
                    Weight = new Weight(exercise.Weight, exercise.Unit),
                    TrainingId = training.Id,
                    Order = index
                };

                scoresToAdd.Add(scoreEntity);
                savedScoreIds.Add(scoreEntity.Id);
                index++;

                var key = $"{exerciseId}-{exercise.Series}";
                if (previousScoresMap.TryGetValue(key, out var previousScore))
                {
                    var eloGain = CalculateEloPerExercise(scoreEntity, previousScore);
                    totalElo += eloGain;
                }
            }

            if (scoresToAdd.Count > 0)
            {
                await _exerciseScoreRepository.AddRangeAsync(scoresToAdd, cancellationToken);
            }

            var trainingScores = savedScoreIds.Select((scoreId, index) => new TrainingExerciseScore
            {
                Id = Id<TrainingExerciseScore>.New(),
                TrainingId = training.Id,
                ExerciseScoreId = scoreId,
                Order = index
            }).ToList();

            if (trainingScores.Count > 0)
            {
                await _trainingExerciseScoreRepository.AddRangeAsync(trainingScores, cancellationToken);
            }

            var eloEntry = await _eloRepository.GetLatestEntryAsync(user.Id, cancellationToken);
            if (eloEntry == null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<TrainingSummaryResult, AppError>.Failure(new InternalServerError(Messages.TryAgain));
            }

            var newElo = totalElo + eloEntry.Elo;
            var currentRank = _rankService.GetCurrentRank(newElo);
            var nextRank = _rankService.GetNextRank(currentRank.Name);

            user.ProfileRank = currentRank.Name;
            await _eloRepository.AddAsync(new global::LgymApi.Domain.Entities.EloRegistry
            {
                Id = Id<LgymApi.Domain.Entities.EloRegistry>.New(),
                UserId = user.Id,
                Date = DateTimeOffset.UtcNow,
                Elo = newElo,
                TrainingId = training.Id
            }, cancellationToken);
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _commandDispatcher.EnqueueAsync(new TrainingCompletedCommand { UserId = user.Id, TrainingId = training.Id });
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var comparison = BuildComparisonReport(exercises, previousScoresMap, exerciseDetailsMap);

            await transaction.CommitAsync(cancellationToken);
            return Result<TrainingSummaryResult, AppError>.Success(new TrainingSummaryResult
            {
                Comparison = comparison,
                GainElo = totalElo,
                UserOldElo = eloEntry.Elo,
                ProfileRank = new Features.User.Models.RankInfo { Name = currentRank.Name, NeedElo = currentRank.NeedElo },
                NextRank = nextRank == null ? null : new Features.User.Models.RankInfo { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
                Message = Messages.Created
            });
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static int CalculateEloPerExercise(ExerciseScore currentScore, ExerciseScore previousScore)
    {
        return PartElo(previousScore.Weight.Value, previousScore.Reps, currentScore.Weight.Value, currentScore.Reps);
    }

    private static int PartElo(double prevWeight, double prevReps, double accWeight, double accReps)
    {
        const int k = 32;

        double GetWeightedScore(double weight, double reps)
        {
            if (weight <= 15)
            {
                return weight * 0.3 + reps * 0.7;
            }

            if (weight <= 80)
            {
                return weight * 0.5 + reps * 0.5;
            }

            return weight * 0.7 + reps * 0.3;
        }

        var prevScore = GetWeightedScore(prevWeight, prevReps);
        var accScore = GetWeightedScore(accWeight, accReps);
        var toleranceThreshold = prevWeight > 80 ? 0.1 * prevScore : 0.05 * prevScore;

        var expectedScore = Math.Abs(accScore - prevScore) <= toleranceThreshold
            ? 0.5
            : prevScore / (prevScore + accScore);

        var actualScore = accScore >= prevScore ? 1 : 0;
        var scoreDifference = (actualScore - expectedScore) * (Math.Abs(accScore - prevScore) < toleranceThreshold ? 0.5 : 1);
        var points = k * scoreDifference;

        return (int)Math.Round(points);
    }

    private async Task<Dictionary<string, ExerciseScore>> FetchPreviousScores(Id<LgymApi.Domain.Entities.User> userId, Id<LgymApi.Domain.Entities.Gym> gymId, List<Id<LgymApi.Domain.Entities.Exercise>> exerciseIds, CancellationToken cancellationToken)
    {
        var scores = await _exerciseScoreRepository.GetByUserAndExercisesAsync(userId, exerciseIds, cancellationToken);
        scores = scores
            .Where(s => s.Training != null && s.Training.GymId == gymId)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        var map = new Dictionary<string, ExerciseScore>();
        foreach (var score in scores)
        {
            var key = $"{score.ExerciseId}-{score.Series}";
            if (!map.ContainsKey(key))
            {
                map[key] = score;
            }
        }

        return map;
    }

    internal List<GroupedExerciseComparison> BuildComparisonReport(
        IReadOnlyCollection<TrainingExerciseInput> currentExercises,
        Dictionary<string, ExerciseScore> previousScores,
        Dictionary<Id<LgymApi.Domain.Entities.Exercise>, string> exerciseDetails)
    {
        var comparisonMap = new Dictionary<Id<LgymApi.Domain.Entities.Exercise>, GroupedExerciseComparison>();

        foreach (var current in currentExercises)
        {
            if (current.ExerciseId.IsEmpty)
            {
                continue;
            }

            var exerciseId = current.ExerciseId;

            if (!comparisonMap.TryGetValue(exerciseId, out var group))
            {
                comparisonMap[exerciseId] = new GroupedExerciseComparison
                {
                    ExerciseId = exerciseId,
                    ExerciseName = exerciseDetails.TryGetValue(exerciseId, out var name) ? name : "Nieznane cwiczenie",
                    SeriesComparisons = new List<SeriesComparison>()
                };
            }

            var key = $"{exerciseId}-{current.Series}";
            previousScores.TryGetValue(key, out var previous);

            comparisonMap[exerciseId].SeriesComparisons.Add(new SeriesComparison
            {
                Series = current.Series,
                CurrentResult = new ScoreResult
                {
                    Reps = current.Reps,
                    Weight = current.Weight,
                    Unit = current.Unit
                },
                        PreviousResult = previous == null
                            ? null
                            : new ScoreResult
                            {
                                Reps = previous.Reps,
                                Weight = previous.Weight.Value,
                                Unit = previous.Weight.Unit
                            }
                    });
        }

        var exerciseOrder = currentExercises
            .Select(e => e.ExerciseId)
            .Where(id => !id.IsEmpty)
            .Distinct()
            .ToList();

        return exerciseOrder
            .Where(comparisonMap.ContainsKey)
            .Select(id => comparisonMap[id])
            .ToList();
    }
}
