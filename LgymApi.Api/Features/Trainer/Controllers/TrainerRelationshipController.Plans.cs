using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Mvc;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.Trainer.Controllers;

public sealed partial class TrainerRelationshipController : ControllerBase
{
    [HttpGet("trainees/{traineeId}/plans")]
    [ProducesResponseType(typeof(List<TrainerManagedPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTraineePlans([FromRoute] string traineeId, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<List<TrainerManagedPlanResult>, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetTraineePlansAsync(trainer!, parsedTraineeId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<TrainerManagedPlanResult, TrainerManagedPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/plans")]
    [ProducesResponseType(typeof(TrainerManagedPlanDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTraineePlan([FromRoute] string traineeId, [FromBody] TrainerPlanFormRequest request, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<TrainerManagedPlanResult, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.CreateTraineePlanAsync(trainer!, parsedTraineeId, request.Name, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return StatusCode(StatusCodes.Status201Created, _mapper.Map<TrainerManagedPlanResult, TrainerManagedPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/plans/{planId}/update")]
    [ProducesResponseType(typeof(TrainerManagedPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTraineePlan([FromRoute] string traineeId, [FromRoute] string planId, [FromBody] TrainerPlanFormRequest request, CancellationToken cancellationToken = default)
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
        var result = await _trainerRelationshipService.UpdateTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, request.Name, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<TrainerManagedPlanResult, TrainerManagedPlanDto>(result.Value));
    }

    [HttpPost("trainees/{traineeId}/plans/{planId}/delete")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteTraineePlan([FromRoute] string traineeId, [FromRoute] string planId, CancellationToken cancellationToken = default)
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
        var result = await _trainerRelationshipService.DeleteTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("trainees/{traineeId}/plans/{planId}/assign")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignTraineePlan([FromRoute] string traineeId, [FromRoute] string planId, CancellationToken cancellationToken = default)
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
        var result = await _trainerRelationshipService.AssignTraineePlanAsync(trainer!, parsedTraineeId, parsedPlanId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("trainees/{traineeId}/plans/unassign")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnassignTraineePlan([FromRoute] string traineeId, CancellationToken cancellationToken = default)
    {
        if (!Id<UserEntity>.TryParse(traineeId, out var parsedTraineeId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.UserIdRequired)).ToActionResult();
        }

        var trainer = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.UnassignTraineePlanAsync(trainer!, parsedTraineeId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }
}
