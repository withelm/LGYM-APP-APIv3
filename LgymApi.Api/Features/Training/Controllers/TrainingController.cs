using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Enum;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.PlanDay.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Training.Controllers;

[ApiController]
[Route("api")]
public sealed class TrainingController : ControllerBase
{
    private readonly ITrainingService _trainingService;
    private readonly IMapper _mapper;

    public TrainingController(ITrainingService trainingService, IMapper mapper)
    {
        _trainingService = trainingService;
        _mapper = mapper;
    }

    [HttpPost("{id}/addTraining")]
    [ProducesResponseType(typeof(TrainingSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddTraining([FromRoute] string id, [FromBody] TrainingFormDto form)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var gymId = Guid.TryParse(form.GymId, out var parsedGymId) ? parsedGymId : Guid.Empty;
        var planDayId = Guid.TryParse(form.TypePlanDayId, out var parsedPlanDayId) ? parsedPlanDayId : Guid.Empty;
        var exercises = form.Exercises.Select(exercise => new TrainingExerciseInput
        {
                ExerciseId = exercise.ExerciseId,
                Series = exercise.Series,
                Reps = exercise.Reps,
                Weight = exercise.Weight,
                Unit = exercise.Unit
            }).ToList();

        var result = await _trainingService.AddTrainingAsync(userId, gymId, planDayId, form.CreatedAt, exercises);

        var comparison = result.Comparison.Select(group => new GroupedExerciseComparisonDto
        {
            ExerciseId = group.ExerciseId.ToString(),
            ExerciseName = group.ExerciseName,
            SeriesComparisons = group.SeriesComparisons.Select(series => new SeriesComparisonDto
            {
                Series = series.Series,
                CurrentResult = new ScoreResultDto
                {
                    Reps = series.CurrentResult.Reps,
                    Weight = series.CurrentResult.Weight,
                    Unit = series.CurrentResult.Unit.ToLookup()
                },
                PreviousResult = series.PreviousResult == null
                    ? null
                    : new ScoreResultDto
                    {
                        Reps = series.PreviousResult.Reps,
                        Weight = series.PreviousResult.Weight,
                        Unit = series.PreviousResult.Unit.ToLookup()
                    }
            }).ToList()
        }).ToList();

        return Ok(new TrainingSummaryDto
        {
            Comparison = comparison,
            GainElo = result.GainElo,
            UserOldElo = result.UserOldElo,
            ProfileRank = result.ProfileRank == null ? null : new RankDto { Name = result.ProfileRank.Name, NeedElo = result.ProfileRank.NeedElo },
            NextRank = result.NextRank == null ? null : new RankDto { Name = result.NextRank.Name, NeedElo = result.NextRank.NeedElo },
            Message = result.Message
        });
    }

    [HttpGet("{id}/getLastTraining")]
    [ProducesResponseType(typeof(LastTrainingInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLastTraining([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var training = await _trainingService.GetLastTrainingAsync(userId);
        return Ok(_mapper.Map<LgymApi.Domain.Entities.Training, LastTrainingInfoDto>(training));
    }

    [HttpPost("{id}/getTrainingByDate")]
    [ProducesResponseType(typeof(List<TrainingByDateDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrainingByDate([FromRoute] string id, [FromBody] TrainingByDateRequestDto request)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var result = await _trainingService.GetTrainingByDateAsync(userId, request.CreatedAt);
        var mapped = result.Select(training => new TrainingByDateDetailsDto
        {
            Id = training.Id.ToString(),
            TypePlanDayId = training.TypePlanDayId.ToString(),
            CreatedAt = training.CreatedAt,
            PlanDay = training.PlanDay == null ? new PlanDayChooseDto() : new PlanDayChooseDto
            {
                Id = training.PlanDay.Id.ToString(),
                Name = training.PlanDay.Name
            },
            Gym = training.Gym,
            Exercises = training.Exercises.Select(exercise => new EnrichedExerciseDto
            {
                ExerciseScoreId = exercise.ExerciseScoreId.ToString(),
                ExerciseDetails = _mapper.Map<LgymApi.Domain.Entities.Exercise, ExerciseResponseDto>(exercise.ExerciseDetails),
                ScoresDetails = exercise.ScoresDetails.Select(score => _mapper.Map<LgymApi.Domain.Entities.ExerciseScore, ExerciseScoreResponseDto>(score)).ToList()
            }).ToList()
        }).ToList();

        return Ok(mapped);
    }

    [HttpGet("{id}/getTrainingDates")]
    [ProducesResponseType(typeof(List<DateTime>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrainingDates([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var dates = await _trainingService.GetTrainingDatesAsync(userId);
        return Ok(dates);
    }
}
