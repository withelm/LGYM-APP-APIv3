using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
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
    private readonly IEloRegistryRepository _eloRepository;
    private readonly IRankService _rankService;
    private readonly IUnitOfWork _unitOfWork;

    public TrainingService(
        IUserRepository userRepository,
        IGymRepository gymRepository,
        ITrainingRepository trainingRepository,
        IExerciseRepository exerciseRepository,
        IExerciseScoreRepository exerciseScoreRepository,
        ITrainingExerciseScoreRepository trainingExerciseScoreRepository,
        IEloRegistryRepository eloRepository,
        IRankService rankService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _gymRepository = gymRepository;
        _trainingRepository = trainingRepository;
        _exerciseRepository = exerciseRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
        _trainingExerciseScoreRepository = trainingExerciseScoreRepository;
        _eloRepository = eloRepository;
        _rankService = rankService;
        _unitOfWork = unitOfWork;
    }

    public async Task<TrainingSummaryResult> AddTrainingAsync(
        Guid userId,
        Guid gymId,
        Guid planDayId,
        DateTime createdAt,
        IReadOnlyCollection<TrainingExerciseInput> exercises)
    {
        try
        {
            if (userId == Guid.Empty || gymId == Guid.Empty || planDayId == Guid.Empty)
            {
                throw AppException.NotFound(Messages.DidntFind);
            }

            var user = await _userRepository.FindByIdAsync(userId);
            if (user == null)
            {
                throw AppException.NotFound(Messages.DidntFind);
            }

            var gym = await _gymRepository.FindByIdAsync(gymId);
            if (gym == null)
            {
                throw AppException.NotFound(Messages.DidntFind);
            }

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var training = new TrainingEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TypePlanDayId = planDayId,
                    CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)),
                    GymId = gym.Id
                };

                await _trainingRepository.AddAsync(training);

                var uniqueExerciseIds = exercises
                    .Select(e => e.ExerciseId)
                    .Where(e => Guid.TryParse(e, out _))
                    .Distinct()
                    .Select(Guid.Parse)
                    .ToList();

                var exerciseDetails = await _exerciseRepository.GetByIdsAsync(uniqueExerciseIds);
                var exerciseDetailsMap = exerciseDetails.ToDictionary(e => e.Id, e => e.Name);

                var previousScoresMap = await FetchPreviousScores(user.Id, gym.Id, uniqueExerciseIds);

                var savedScoreIds = new List<Guid>();
                var totalElo = 0;
                var scoresToAdd = new List<ExerciseScore>();
                foreach (var exercise in exercises)
                {
                    if (!Guid.TryParse(exercise.ExerciseId, out var exerciseId))
                    {
                        continue;
                    }

                    var unit = ParseWeightUnit(exercise.Unit);
                    var scoreEntity = new ExerciseScore
                    {
                        Id = Guid.NewGuid(),
                        ExerciseId = exerciseId,
                        UserId = user.Id,
                        Reps = exercise.Reps,
                        Series = exercise.Series,
                        Weight = exercise.Weight,
                        Unit = unit,
                        TrainingId = training.Id
                    };

                    scoresToAdd.Add(scoreEntity);
                    savedScoreIds.Add(scoreEntity.Id);

                    var key = $"{exerciseId}-{exercise.Series}";
                    if (previousScoresMap.TryGetValue(key, out var previousScore))
                    {
                        var eloGain = CalculateEloPerExercise(scoreEntity, previousScore);
                        totalElo += eloGain;
                    }
                }

                if (scoresToAdd.Count > 0)
                {
                    await _exerciseScoreRepository.AddRangeAsync(scoresToAdd);
                }

                var trainingScores = savedScoreIds.Select(scoreId => new TrainingExerciseScore
                {
                    Id = Guid.NewGuid(),
                    TrainingId = training.Id,
                    ExerciseScoreId = scoreId
                }).ToList();

                if (trainingScores.Count > 0)
                {
                    await _trainingExerciseScoreRepository.AddRangeAsync(trainingScores);
                }

                var eloEntry = await _eloRepository.GetLatestEntryAsync(user.Id);
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
                });
                await _userRepository.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                var comparison = BuildComparisonReport(exercises, previousScoresMap, exerciseDetailsMap);

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
                await transaction.RollbackAsync();
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

    public async Task<TrainingEntity> GetLastTrainingAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var training = await _trainingRepository.GetLastByUserIdAsync(user.Id);
        if (training == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return training;
    }

    public async Task<List<TrainingByDateDetails>> GetTrainingByDateAsync(Guid userId, DateTime createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var startOfDay = new DateTimeOffset(DateTime.SpecifyKind(createdAt.Date, DateTimeKind.Utc));
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        var trainings = await _trainingRepository.GetByUserIdAndDateAsync(user.Id, startOfDay, endOfDay);
        if (trainings.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var trainingIds = trainings.Select(t => t.Id).ToList();
        var trainingScoreRefs = await _trainingExerciseScoreRepository.GetByTrainingIdsAsync(trainingIds);

        var scoreIds = trainingScoreRefs.Select(t => t.ExerciseScoreId).Distinct().ToList();
        var scores = await _exerciseScoreRepository.GetByIdsAsync(scoreIds);
        var scoreMap = scores.ToDictionary(s => s.Id, s => s);

        var result = new List<TrainingByDateDetails>();
        foreach (var training in trainings)
        {
            var exerciseRefs = trainingScoreRefs.Where(t => t.TrainingId == training.Id).ToList();
            var grouped = new Dictionary<Guid, EnrichedExercise>();

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
                        ExerciseDetails = score.Exercise
                    };
                    grouped[score.ExerciseId] = group;
                }

                group.ScoresDetails.Add(score);
            }

            result.Add(new TrainingByDateDetails
            {
                Id = training.Id,
                TypePlanDayId = training.TypePlanDayId,
                CreatedAt = training.CreatedAt.UtcDateTime,
                PlanDay = training.PlanDay,
                Gym = training.Gym?.Name,
                Exercises = grouped.Values.ToList()
            });
        }

        return result;
    }

    public async Task<List<DateTime>> GetTrainingDatesAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var trainings = await _trainingRepository.GetDatesByUserIdAsync(userId);
        if (trainings.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return trainings.Select(t => t.UtcDateTime).ToList();
    }

    private static int CalculateEloPerExercise(ExerciseScore currentScore, ExerciseScore previousScore)
    {
        return PartElo(previousScore.Weight, previousScore.Reps, currentScore.Weight, currentScore.Reps);
    }

    private static int PartElo(double prevWeight, int prevReps, double accWeight, int accReps)
    {
        const int k = 32;

        double GetWeightedScore(double weight, int reps)
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

    private async Task<Dictionary<string, ExerciseScore>> FetchPreviousScores(Guid userId, Guid gymId, List<Guid> exerciseIds)
    {
        var scores = await _exerciseScoreRepository.GetByUserAndExercisesAsync(userId, exerciseIds);
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

    private List<GroupedExerciseComparison> BuildComparisonReport(
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

            var currentUnit = ParseWeightUnit(current.Unit);
            comparisonMap[exerciseId].SeriesComparisons.Add(new SeriesComparison
            {
                Series = current.Series,
                CurrentResult = new ScoreResult
                {
                    Reps = current.Reps,
                    Weight = current.Weight,
                    Unit = currentUnit
                },
                PreviousResult = previous == null
                    ? null
                    : new ScoreResult
                    {
                        Reps = previous.Reps,
                        Weight = previous.Weight,
                        Unit = previous.Unit
                    }
            });
        }

        return comparisonMap.Values.ToList();
    }

    private static WeightUnits ParseWeightUnit(string? unit)
    {
        if (!string.IsNullOrWhiteSpace(unit) && global::System.Enum.TryParse(unit, true, out WeightUnits parsed))
        {
            return parsed;
        }

        return WeightUnits.Unknown;
    }
}
