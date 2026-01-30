using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Api.Services;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class TrainingController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IGymRepository _gymRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly IExerciseRepository _exerciseRepository;
    private readonly IExerciseScoreRepository _exerciseScoreRepository;
    private readonly ITrainingExerciseScoreRepository _trainingExerciseScoreRepository;
    private readonly IEloRegistryRepository _eloRepository;
    private readonly IRankService _rankService;

    public TrainingController(
        IUserRepository userRepository,
        IGymRepository gymRepository,
        ITrainingRepository trainingRepository,
        IExerciseRepository exerciseRepository,
        IExerciseScoreRepository exerciseScoreRepository,
        ITrainingExerciseScoreRepository trainingExerciseScoreRepository,
        IEloRegistryRepository eloRepository,
        IRankService rankService)
    {
        _userRepository = userRepository;
        _gymRepository = gymRepository;
        _trainingRepository = trainingRepository;
        _exerciseRepository = exerciseRepository;
        _exerciseScoreRepository = exerciseScoreRepository;
        _trainingExerciseScoreRepository = trainingExerciseScoreRepository;
        _eloRepository = eloRepository;
        _rankService = rankService;
    }

    [HttpPost("{id}/addTraining")]
    public async Task<IActionResult> AddTraining([FromRoute] string id, [FromBody] TrainingFormDto form)
    {
        try
        {
            if (!Guid.TryParse(id, out var userId) || !Guid.TryParse(form.GymId, out var gymId))
            {
                return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
            }

            if (!Guid.TryParse(form.TypePlanDayId, out var planDayId))
            {
                return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
            }

            var user = await _userRepository.FindByIdAsync(userId);
            if (user == null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
            }

            var gym = await _gymRepository.FindByIdAsync(gymId);
            if (gym == null)
            {
                return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
            }

            var training = new Training
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TypePlanDayId = planDayId,
                CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(form.CreatedAt, DateTimeKind.Utc)),
                GymId = gym.Id
            };

            await _trainingRepository.AddAsync(training);

            var uniqueExerciseIds = form.Exercises
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
            foreach (var exercise in form.Exercises)
            {
                if (!Guid.TryParse(exercise.ExerciseId, out var exerciseId))
                {
                    continue;
                }

                var unit = exercise.Unit == "lbs" ? WeightUnits.Pounds : WeightUnits.Kilograms;
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
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseMessageDto { Message = Messages.TryAgain });
            }

            var newElo = totalElo + eloEntry.Elo;
            var currentRank = _rankService.GetCurrentRank(newElo);
            var nextRank = _rankService.GetNextRank(currentRank.Name);

            user.ProfileRank = currentRank.Name;
            await _eloRepository.AddAsync(new EloRegistry
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Date = DateTimeOffset.UtcNow,
                Elo = newElo,
                TrainingId = training.Id
            });
            await _userRepository.UpdateAsync(user);

            var comparison = BuildComparisonReport(form.Exercises, previousScoresMap, exerciseDetailsMap);

            return Ok(new TrainingSummaryDto
            {
                Comparison = comparison,
                GainElo = totalElo,
                UserOldElo = eloEntry.Elo,
                ProfileRank = new RankDto { Name = currentRank.Name, NeedElo = currentRank.NeedElo },
                NextRank = nextRank == null ? null : new RankDto { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
                Message = Messages.Created
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ResponseMessageDto { Message = Messages.TryAgain });
        }
    }

    [HttpGet("{id}/getLastTraining")]
    public async Task<IActionResult> GetLastTraining([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var training = await _trainingRepository.GetLastByUserIdAsync(user.Id);

        if (training == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var result = new LastTrainingInfoDto
        {
            Id = training.Id.ToString(),
            TypePlanDayId = training.TypePlanDayId.ToString(),
            CreatedAt = training.CreatedAt.UtcDateTime,
            PlanDay = training.PlanDay == null ? new PlanDayChooseDto() : new PlanDayChooseDto
            {
                Id = training.PlanDay.Id.ToString(),
                Name = training.PlanDay.Name
            }
        };

        return Ok(result);
    }

    [HttpPost("{id}/getTrainingByDate")]
    public async Task<IActionResult> GetTrainingByDate([FromRoute] string id, [FromBody] TrainingByDateRequestDto request)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = await _userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var startOfDay = new DateTimeOffset(DateTime.SpecifyKind(request.CreatedAt.Date, DateTimeKind.Utc));
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        var trainings = await _trainingRepository.GetByUserIdAndDateAsync(user.Id, startOfDay, endOfDay);

        if (trainings.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var trainingIds = trainings.Select(t => t.Id).ToList();
        var trainingScoreRefs = await _trainingExerciseScoreRepository.GetByTrainingIdsAsync(trainingIds);

        var scoreIds = trainingScoreRefs.Select(t => t.ExerciseScoreId).Distinct().ToList();
        var scores = await _exerciseScoreRepository.GetByIdsAsync(scoreIds);

        var scoreMap = scores.ToDictionary(s => s.Id, s => s);

        var result = new List<TrainingByDateDetailsDto>();
        foreach (var training in trainings)
        {
            var exerciseRefs = trainingScoreRefs.Where(t => t.TrainingId == training.Id).ToList();
            var grouped = new Dictionary<Guid, EnrichedExerciseDto>();

            foreach (var reference in exerciseRefs)
            {
                if (!scoreMap.TryGetValue(reference.ExerciseScoreId, out var score) || score.Exercise == null)
                {
                    continue;
                }

                if (!grouped.TryGetValue(score.ExerciseId, out var group))
                {
                    group = new EnrichedExerciseDto
                    {
                        ExerciseScoreId = reference.ExerciseScoreId.ToString(),
                        ExerciseDetails = new ExerciseResponseDto
                        {
                            Id = score.Exercise.Id.ToString(),
                            Name = score.Exercise.Name,
                            BodyPart = score.Exercise.BodyPart.ToLookup()
                        }
                    };
                    grouped[score.ExerciseId] = group;
                }

                group.ScoresDetails.Add(new ExerciseScoreResponseDto
                {
                    Id = score.Id.ToString(),
                    ExerciseId = score.ExerciseId.ToString(),
                    Reps = score.Reps,
                    Series = score.Series,
                    Weight = score.Weight,
                    Unit = score.Unit.ToLookup()
                });
            }

            result.Add(new TrainingByDateDetailsDto
            {
                Id = training.Id.ToString(),
                TypePlanDayId = training.TypePlanDayId.ToString(),
                CreatedAt = training.CreatedAt.UtcDateTime,
                PlanDay = training.PlanDay == null ? new PlanDayChooseDto() : new PlanDayChooseDto
                {
                    Id = training.PlanDay.Id.ToString(),
                    Name = training.PlanDay.Name
                },
                Gym = training.Gym?.Name,
                Exercises = grouped.Values.ToList()
            });
        }

        return Ok(result);
    }

    [HttpGet("{id}/getTrainingDates")]
    public async Task<IActionResult> GetTrainingDates([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var trainings = await _trainingRepository.GetDatesByUserIdAsync(userId);

        if (trainings.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        return Ok(trainings.Select(t => t.UtcDateTime).ToList());
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

    private List<GroupedExerciseComparisonDto> BuildComparisonReport(
        List<ExerciseScoresTrainingFormDto> currentExercises,
        Dictionary<string, ExerciseScore> previousScores,
        Dictionary<Guid, string> exerciseDetails)
    {
        var comparisonMap = new Dictionary<Guid, GroupedExerciseComparisonDto>();

        foreach (var current in currentExercises)
        {
            if (!Guid.TryParse(current.ExerciseId, out var exerciseId))
            {
                continue;
            }

            if (!comparisonMap.TryGetValue(exerciseId, out var group))
            {
                comparisonMap[exerciseId] = new GroupedExerciseComparisonDto
                {
                    ExerciseId = current.ExerciseId,
                    ExerciseName = exerciseDetails.TryGetValue(exerciseId, out var name) ? name : "Nieznane cwiczenie",
                    SeriesComparisons = new List<SeriesComparisonDto>()
                };
            }

            var key = $"{exerciseId}-{current.Series}";
            previousScores.TryGetValue(key, out var previous);

            var currentUnit = current.Unit == "lbs" ? WeightUnits.Pounds : WeightUnits.Kilograms;
            comparisonMap[exerciseId].SeriesComparisons.Add(new SeriesComparisonDto
            {
                Series = current.Series,
                CurrentResult = new ScoreResultDto
                {
                    Reps = current.Reps,
                    Weight = current.Weight,
                    Unit = currentUnit.ToLookup()
                },
                PreviousResult = previous == null
                    ? null
                    : new ScoreResultDto
                    {
                        Reps = previous.Reps,
                        Weight = previous.Weight,
                        Unit = previous.Unit.ToLookup()
                    }
            });
        }

        return comparisonMap.Values.ToList();
    }
}
