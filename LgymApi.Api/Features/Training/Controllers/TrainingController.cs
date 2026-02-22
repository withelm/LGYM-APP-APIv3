using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Training.Contracts;
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

        var result = await _trainingService.AddTrainingAsync(userId, gymId, planDayId, form.CreatedAt, exercises, HttpContext.RequestAborted);
        var mapped = _mapper.Map<TrainingSummaryResult, TrainingSummaryDto>(result);
        return Ok(mapped);
    }

    [HttpGet("{id}/getLastTraining")]
    [ProducesResponseType(typeof(LastTrainingInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLastTraining([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var training = await _trainingService.GetLastTrainingAsync(userId, HttpContext.RequestAborted);
        return Ok(_mapper.Map<LgymApi.Domain.Entities.Training, LastTrainingInfoDto>(training));
    }

    [HttpPost("{id}/getTrainingByDate")]
    [ProducesResponseType(typeof(List<TrainingByDateDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrainingByDate([FromRoute] string id, [FromBody] TrainingByDateRequestDto request)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var result = await _trainingService.GetTrainingByDateAsync(userId, request.CreatedAt, HttpContext.RequestAborted);
        var mapped = _mapper.MapList<TrainingByDateDetails, TrainingByDateDetailsDto>(result);
        return Ok(mapped);
    }

    [HttpGet("{id}/getTrainingDates")]
    [ProducesResponseType(typeof(List<DateTime>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrainingDates([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var dates = await _trainingService.GetTrainingDatesAsync(userId, HttpContext.RequestAborted);
        return Ok(dates);
    }
}
