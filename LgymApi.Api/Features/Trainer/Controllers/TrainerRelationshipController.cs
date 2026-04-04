using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.EloRegistry.Contracts;
using LgymApi.Api.Features.ExerciseScores.Contracts;
using LgymApi.Api.Features.MainRecords.Contracts;
using LgymApi.Api.Features.Training.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

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
    [ApiIdempotency("/api/trainer/invitations", ApiIdempotencyScopeSource.AuthenticatedUser)]
    [ProducesResponseType(typeof(TrainerInvitationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateTrainerInvitationRequest request)
    {
        if (!Id<UserEntity>.TryParse(request.TraineeId, out var traineeId))
        {
            return Result<TrainerInvitationResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.CreateInvitationAsync(trainer!, traineeId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<LgymApi.Application.Features.TrainerRelationships.Models.TrainerInvitationResult, TrainerInvitationDto>(result.Value));
    }

    [HttpGet("invitations")]
    [ProducesResponseType(typeof(List<TrainerInvitationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitations()
    {
        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTrainerInvitationsAsync(trainer!, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<LgymApi.Application.Features.TrainerRelationships.Models.TrainerInvitationResult, TrainerInvitationDto>(result.Value));
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
        }, HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(new TrainerDashboardTraineesResponse
        {
            Page = result.Value.Page,
            PageSize = result.Value.PageSize,
            Total = result.Value.Total,
            Items = _mapper.MapList<TrainerDashboardTraineeResult, TrainerDashboardTraineeDto>(result.Value.Items)
        });
    }

    [HttpGet("trainees/{traineeId}/trainings/dates")]
    [ProducesResponseType(typeof(List<DateTime>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeTrainingDates([FromRoute] string traineeId)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<DateTime>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeTrainingDatesAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(result.Value);
    }

    [HttpPost("trainees/{traineeId}/trainings/by-date")]
    [ProducesResponseType(typeof(List<TrainingByDateDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeTrainingByDate([FromRoute] string traineeId, [FromBody] TrainingByDateRequestDto request)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<TrainingByDateDetails>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeTrainingByDateAsync(trainer!, parsedTraineeId, request.CreatedAt, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<TrainingByDateDetails, TrainingByDateDetailsDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/exercise-scores/chart")]
    [ProducesResponseType(typeof(List<ExerciseScoresChartDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeExerciseScoresChartData([FromRoute] string traineeId, [FromBody] ExerciseScoresChartRequestDto request)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        if (!Id<ExerciseEntity>.TryParse(request.ExerciseId, out var parsedExerciseId))
        {
            return Result<List<ExerciseScoresChartData>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.ExerciseIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeExerciseScoresChartDataAsync(trainer!, parsedTraineeId, parsedExerciseId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<ExerciseScoresChartData, ExerciseScoresChartDataDto>(result.Value));
    }

    [HttpGet("trainees/{traineeId}/elo/chart")]
    [ProducesResponseType(typeof(List<EloRegistryBaseChartDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeEloChart([FromRoute] string traineeId)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<EloRegistryChartEntry>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeEloChartAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<EloRegistryChartEntry, EloRegistryBaseChartDto>(result.Value));
    }

    [HttpGet("trainees/{traineeId}/main-records/history")]
    [ProducesResponseType(typeof(List<MainRecordResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeMainRecordsHistory([FromRoute] string traineeId)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<LgymApi.Domain.Entities.MainRecord>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineeMainRecordsHistoryAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mappedRecords = _mapper.MapList<LgymApi.Domain.Entities.MainRecord, MainRecordResponseDto>(result.Value);
        return Ok(mappedRecords);
    }

    [HttpPost("trainees/{traineeId}/unlink")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlinkTrainee([FromRoute] string traineeId)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.UnlinkTraineeAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("trainees/{traineeId}/plans")]
    [ProducesResponseType(typeof(List<TrainerManagedPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTraineePlans([FromRoute] string traineeId)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<TrainerManagedPlanResult>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineePlansAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<TrainerManagedPlanResult, TrainerManagedPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/plans")]
    [ProducesResponseType(typeof(TrainerManagedPlanDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTraineePlan([FromRoute] string traineeId, [FromBody] TrainerPlanFormRequest request)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.CreateTraineePlanAsync(trainer!, parsedTraineeId, request.Name, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return StatusCode(StatusCodes.Status201Created, _mapper.Map<TrainerManagedPlanResult, TrainerManagedPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/plans/{planId}/update")]
    [ProducesResponseType(typeof(TrainerManagedPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTraineePlan([FromRoute] string traineeId, [FromRoute] string planId, [FromBody] TrainerPlanFormRequest request)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        if (!Id<PlanEntity>.TryParse(planId, out var parsedPlanId))
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.UpdateTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, request.Name, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<TrainerManagedPlanResult, TrainerManagedPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/plans/{planId}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteTraineePlan([FromRoute] string traineeId, [FromRoute] string planId)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        if (!Id<PlanEntity>.TryParse(planId, out var parsedPlanId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.DeleteTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("trainees/{traineeId}/plans/{planId}/assign")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignTraineePlan([FromRoute] string traineeId, [FromRoute] string planId)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        if (!Id<PlanEntity>.TryParse(planId, out var parsedPlanId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.AssignTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("trainees/{traineeId}/plans/unassign")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnassignTraineePlan([FromRoute] string traineeId)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.UnassignTraineePlanAsync(trainer!, parsedTraineeId, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }
}
