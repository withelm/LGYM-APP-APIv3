using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.EloRegistry.Contracts;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Api.Features.PlanDay.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainer")]
[Authorize(Policy = AuthConstants.Policies.TrainerAccess)]
public sealed class TrainerRelationshipController : ControllerBase
{
    private readonly ITrainerRelationshipService _trainerRelationshipService;
    private readonly IMapper _mapper;

    public TrainerRelationshipController(ITrainerRelationshipService trainerRelationshipService, IMapper mapper)
    {
        _trainerRelationshipService = trainerRelationshipService;
        _mapper = mapper;
    }

    [HttpPost("invitations")]
    [ProducesResponseType(typeof(TrainerInvitationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateTrainerInvitationRequest request)
    {
        if (!Guid.TryParse(request.TraineeId, out var traineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var invitation = await _trainerRelationshipService.CreateInvitationAsync(trainer!, traineeId);
        return Ok(_mapper.Map<LgymApi.Application.Features.TrainerRelationships.Models.TrainerInvitationResult, TrainerInvitationDto>(invitation));
    }

    [HttpGet("invitations")]
    [ProducesResponseType(typeof(List<TrainerInvitationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitations()
    {
        var trainer = HttpContext.GetCurrentUser();
        var invitations = await _trainerRelationshipService.GetTrainerInvitationsAsync(trainer!);
        return Ok(_mapper.MapList<LgymApi.Application.Features.TrainerRelationships.Models.TrainerInvitationResult, TrainerInvitationDto>(invitations));
    }

    [HttpGet("trainees")]
    [ProducesResponseType(typeof(TrainerDashboardTraineesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardTrainees([FromQuery] TrainerDashboardTraineesRequest request)
    {
        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetDashboardTraineesAsync(trainer!, new TrainerDashboardTraineeQuery
        {
            Search = request.Search,
            Status = request.Status,
            SortBy = request.SortBy,
            SortDirection = request.SortDirection,
            Page = request.Page,
            PageSize = request.PageSize
        });

        return Ok(new TrainerDashboardTraineesResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            Total = result.Total,
            Items = _mapper.MapList<TrainerDashboardTraineeResult, TrainerDashboardTraineeDto>(result.Items)
        });
    }

    [HttpGet("trainees/{traineeId}/trainings/dates")]
    [ProducesResponseType(typeof(List<DateTime>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeTrainingDates([FromRoute] string traineeId)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var dates = await _trainerRelationshipService.GetTraineeTrainingDatesAsync(trainer!, parsedTraineeId);
        return Ok(dates);
    }

    [HttpPost("trainees/{traineeId}/trainings/by-date")]
    [ProducesResponseType(typeof(List<TrainingByDateDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeTrainingByDate([FromRoute] string traineeId, [FromBody] TrainingByDateRequestDto request)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeTrainingByDateAsync(trainer!, parsedTraineeId, request.CreatedAt);
        var mapped = result.Select(training => new TrainingByDateDetailsDto
        {
            Id = training.Id.ToString(),
            TypePlanDayId = training.TypePlanDayId.ToString(),
            CreatedAt = training.CreatedAt,
            PlanDay = training.PlanDay == null
                ? new PlanDayChooseDto()
                : new PlanDayChooseDto
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

    [HttpPost("trainees/{traineeId}/exercise-scores/chart")]
    [ProducesResponseType(typeof(List<ExerciseScoresChartDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeExerciseScoresChartData([FromRoute] string traineeId, [FromBody] ExerciseScoresChartRequestDto request)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        if (!Guid.TryParse(request.ExerciseId, out var parsedExerciseId))
        {
            throw AppException.BadRequest(Messages.ExerciseIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeExerciseScoresChartDataAsync(trainer!, parsedTraineeId, parsedExerciseId);
        var mapped = result.Select(entry => new ExerciseScoresChartDataDto
        {
            Id = entry.Id,
            Value = entry.Value,
            Date = entry.Date,
            ExerciseName = entry.ExerciseName,
            ExerciseId = entry.ExerciseId
        }).ToList();

        return Ok(mapped);
    }

    [HttpGet("trainees/{traineeId}/elo/chart")]
    [ProducesResponseType(typeof(List<EloRegistryBaseChartDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeEloChart([FromRoute] string traineeId)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeEloChartAsync(trainer!, parsedTraineeId);
        var mapped = result.Select(entry => new EloRegistryBaseChartDto
        {
            Id = entry.Id,
            Value = entry.Value,
            Date = entry.Date
        }).ToList();

        return Ok(mapped);
    }

    [HttpGet("trainees/{traineeId}/main-records/history")]
    [ProducesResponseType(typeof(List<MainRecordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeMainRecordsHistory([FromRoute] string traineeId)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        var records = await _trainerRelationshipService.GetTraineeMainRecordsHistoryAsync(trainer!, parsedTraineeId);
        var mappedRecords = _mapper.MapList<LgymApi.Domain.Entities.MainRecord, MainRecordResponseDto>(records);
        return Ok(mappedRecords);
    }

    [HttpPost("trainees/{traineeId}/unlink")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlinkTrainee([FromRoute] string traineeId)
    {
        if (!Guid.TryParse(traineeId, out var parsedTraineeId))
        {
            throw AppException.BadRequest(Messages.UserIdRequired);
        }

        var trainer = HttpContext.GetCurrentUser();
        await _trainerRelationshipService.UnlinkTraineeAsync(trainer!, parsedTraineeId);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }
}
