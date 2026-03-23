using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Application.Units;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using TrainingEntity = LgymApi.Domain.Entities.Training;

namespace LgymApi.Application.Features.Training;

public sealed class TrainingService : ITrainingService
{

    private readonly IUserRepository _userRepository;
    private readonly IGymRepository _gymRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;
    private readonly ITrainingExerciseScoreRepository _trainingExerciseScoreRepository;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IEloRegistryRepository _eloRepository;
    private readonly IRankService _rankService;
    private readonly IUnitOfWork _unitOfWork;

    public TrainingService(ITrainingServiceDependencies dependencies)
    {
        _userRepository = dependencies.UserRepository;
        _gymRepository = dependencies.GymRepository;
        _trainingRepository = dependencies.TrainingRepository;
        _exerciseRepository = dependencies.ExerciseRepository;
        _exerciseScoreRepository = dependencies.ExerciseScoreRepository;
        _trainingExerciseScoreRepository = dependencies.TrainingExerciseScoreRepository;
        _commandDispatcher = dependencies.CommandDispatcher;
        _eloRepository = dependencies.EloRepository;
        _rankService = dependencies.RankService;
        _unitOfWork = dependencies.UnitOfWork;
    }

    public async Task<TrainingSummaryResult> AddTrainingAsync(
        Guid userId,
        AddTrainingInput input,
        CancellationToken cancellationToken = default)
    {
        var (gymId, planDayId, createdAt, exercises) = input;
        try
        {
            if (userId == Guid.Empty || gymId == Guid.Empty || planDayId == Guid.Empty)
            {
                throw AppException.NotFound(Messages.DidntFind);
            }

            var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                throw AppException.NotFound(Messages.DidntFind);
            }

            var gym = await _gymRepository.FindByIdAsync(gymId, cancellationToken);
            if (gym == null)
            {
                throw AppException.NotFound(Messages.DidntFind);
            }

            var uniqueExerciseIds = exercises
                .Select(e => e.ExerciseId)
                .Where(e => Guid.TryParse(e, out _))
                .Distinct()
                .Select(Guid.Parse)
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
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TypePlanDayId = planDayId,
                    CreatedAt = new DateTimeOffset(createdAtUtc),
                    GymId = gym.Id
                };

                await _trainingRepository.AddAsync(training, cancellationToken);

                var savedScoreIds = new List<Guid>();
                var totalElo = 0;
                var scoresToAdd = new List<ExerciseScore>();
                var index = 0;
                foreach (var exercise in exercises)
                {
                    if (!Guid.TryParse(exercise.ExerciseId, out var exerciseId))
                    {
                        continue;
                    }

                    if (exercise.Unit == WeightUnits.Unknown)
                    {
                        throw AppException.BadRequest(Messages.FieldRequired);
                    }

                    var scoreEntity = new ExerciseScore
                    {
                        Id = Guid.NewGuid(),
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
                    Id = Guid.NewGuid(),
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
                    throw AppException.Internal(Messages.TryAgain);
                }

                var newElo = totalElo + eloEntry.Elo;
                var currentRank = _rankService.GetCurrentRank(newElo);
                var nextRank = _rankService.GetNextRank(currentRank.Name);

                user.ProfileRank = currentRank.Name;
                await _eloRepository.AddAsync(new global::LgymApi.Domain.Entities.EloRegistry
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Date = DateTimeOffset.UtcNow,
                    Elo = newElo,
                    TrainingId = training.Id
                }, cancellationToken);
                await _userRepository.UpdateAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var comparison = BuildComparisonReport(exercises, previousScoresMap, exerciseDetailsMap);

                await _commandDispatcher.EnqueueAsync(new TrainingCompletedCommand { UserId = user.Id, TrainingId = training.Id });

                await transaction.CommitAsync(cancellationToken);
                return new TrainingSummaryResult
                {
                    Comparison = comparison,
                    GainElo = totalElo,
                    UserOldElo = eloEntry.Elo,
                    ProfileRank = new Features.User.Models.RankInfo { Name = currentRank.Name, NeedElo = currentRank.NeedElo },
                    NextRank = nextRank == null ? null : new Features.User.Models.RankInfo { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
                    Message = Messages.Created
                };
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not AppException)
        {
            throw AppException.Internal(Messages.TryAgain);
        }
    }



    public async Task<TrainingEntity> GetLastTrainingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var training = await _trainingRepository.GetLastByUserIdAsync(user.Id, cancellationToken);
        if (training == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return training;
    }

    public async Task<List<TrainingByDateDetails>> GetTrainingByDateAsync(Guid userId, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var startOfDay = new DateTimeOffset(DateTime.SpecifyKind(createdAt.Date, DateTimeKind.Utc));
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        var trainings = await _trainingRepository.GetByUserIdAndDateAsync(user.Id, startOfDay, endOfDay, cancellationToken);
        if (trainings.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
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
            var grouped = new Dictionary<Guid, EnrichedExercise>();
            var exerciseOrderMap = new Dictionary<Guid, int>();

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

        return result;
    }

    public async Task<List<DateTime>> GetTrainingDatesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var trainings = await _trainingRepository.GetDatesByUserIdAsync(userId, cancellationToken);
        if (trainings.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return trainings.Select(t => t.UtcDateTime).ToList();
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

    private async Task<Dictionary<string, ExerciseScore>> FetchPreviousScores(Guid userId, Guid gymId, List<Guid> exerciseIds, CancellationToken cancellationToken)
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
        Dictionary<Guid, string> exerciseDetails)
    {
        var comparisonMap = new Dictionary<Guid, GroupedExerciseComparison>();

        foreach (var current in currentExercises)
        {
            if (!Guid.TryParse(current.ExerciseId, out var exerciseId))
            {
                continue;
            }

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
            .Where(id => Guid.TryParse(id, out _))
            .Select(id => Guid.Parse(id))
            .Distinct()
            .ToList();

        return exerciseOrder
            .Where(comparisonMap.ContainsKey)
            .Select(id => comparisonMap[id])
            .ToList();
    }

}
