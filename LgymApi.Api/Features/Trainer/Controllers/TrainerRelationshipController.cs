using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.TrainerRelationships;
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
