using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainerInvitationEntity = LgymApi.Domain.Entities.TrainerInvitation;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainee")]
[Authorize]
public sealed class TraineeRelationshipController : ControllerBase
{
    private readonly ITrainerRelationshipService _trainerRelationshipService;
    private readonly IMapper _mapper;

    public TraineeRelationshipController(ITrainerRelationshipService trainerRelationshipService, IMapper mapper)
    {
        _trainerRelationshipService = trainerRelationshipService;
        _mapper = mapper;
    }

    [HttpGet("invitations")]
    [ProducesResponseType(typeof(List<TrainerInvitationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingInvitations(CancellationToken cancellationToken = default)
    {
        var trainee = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetPendingInvitationsForTraineeAsync(trainee!, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.MapList<TrainerInvitationResult, TrainerInvitationDto>(result.Value));
    }

    [HttpPost("invitations/{invitationId}/accept")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptInvitation([FromRoute] string invitationId, CancellationToken cancellationToken = default)
    {
        if (!Id<TrainerInvitationEntity>.TryParse(invitationId, out var parsedInvitationId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired)).ToActionResult();
        }

        var trainee = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.AcceptInvitationAsync(trainee!, parsedInvitationId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("invitations/{invitationId}/reject")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectInvitation([FromRoute] string invitationId, CancellationToken cancellationToken = default)
    {
        if (!Id<TrainerInvitationEntity>.TryParse(invitationId, out var parsedInvitationId))
        {
            return Result<Unit, AppError>.Failure(new InvalidTrainerRelationshipError(Messages.FieldRequired)).ToActionResult();
        }

        var trainee = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.RejectInvitationAsync(trainee!, parsedInvitationId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("trainer/detach")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetachFromTrainer(CancellationToken cancellationToken = default)
    {
        var trainee = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.DetachFromTrainerAsync(trainee!, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("plan/active")]
    [ProducesResponseType(typeof(TrainerManagedPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveAssignedPlan(CancellationToken cancellationToken = default)
    {
        var trainee = HttpContext.GetCurrentUser();
        var result = await _trainerRelationshipService.GetActiveAssignedPlanAsync(trainee!, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<TrainerManagedPlanResult, TrainerManagedPlanDto>(result.Value));
    }
}
