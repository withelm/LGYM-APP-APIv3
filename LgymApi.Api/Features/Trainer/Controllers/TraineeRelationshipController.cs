using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.TrainerRelationships;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainee")]
[Authorize]
public sealed class TraineeRelationshipController : ControllerBase
{
    private readonly ITrainerRelationshipService _trainerRelationshipService;

    public TraineeRelationshipController(ITrainerRelationshipService trainerRelationshipService)
    {
        _trainerRelationshipService = trainerRelationshipService;
    }

    [HttpPost("invitations/{invitationId}/accept")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptInvitation([FromRoute] string invitationId)
    {
        if (!Guid.TryParse(invitationId, out var parsedInvitationId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainee = HttpContext.GetCurrentUser();
        await _trainerRelationshipService.AcceptInvitationAsync(trainee!, parsedInvitationId);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpPost("invitations/{invitationId}/reject")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectInvitation([FromRoute] string invitationId)
    {
        if (!Guid.TryParse(invitationId, out var parsedInvitationId))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var trainee = HttpContext.GetCurrentUser();
        await _trainerRelationshipService.RejectInvitationAsync(trainee!, parsedInvitationId);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpPost("trainer/detach")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetachFromTrainer()
    {
        var trainee = HttpContext.GetCurrentUser();
        await _trainerRelationshipService.DetachFromTrainerAsync(trainee!);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }
}
