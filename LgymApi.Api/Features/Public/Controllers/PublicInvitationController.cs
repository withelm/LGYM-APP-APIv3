using LgymApi.Api.Features.Public.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainerInvitationEntity = LgymApi.Domain.Entities.TrainerInvitation;

namespace LgymApi.Api.Features.Public.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/invitations")]
public sealed class PublicInvitationController : ControllerBase
{
    private readonly ITrainerRelationshipRepository _trainerRelationshipRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public PublicInvitationController(ITrainerRelationshipRepository trainerRelationshipRepository, IUserRepository userRepository, IMapper mapper)
    {
        _trainerRelationshipRepository = trainerRelationshipRepository;
        _userRepository = userRepository;
        _mapper = mapper;
    }

    [HttpGet("{invitationId}")]
    [ProducesResponseType(typeof(PublicInvitationStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvitationStatus([FromRoute] string invitationId, [FromQuery] string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return NotFound();
        }

        if (!Id<TrainerInvitationEntity>.TryParse(invitationId, out var parsedId))
        {
            return NotFound();
        }

        var invitation = await _trainerRelationshipRepository.FindInvitationByIdWithCodeAsync(parsedId, code, HttpContext.RequestAborted);
        if (invitation == null)
        {
            return NotFound();
        }

        bool userExists = invitation.TraineeId != null;
        if (!userExists && !string.IsNullOrWhiteSpace(invitation.InviteeEmail))
        {
            userExists = await _userRepository.FindByEmailAsync(invitation.InviteeEmail, HttpContext.RequestAborted) != null;
        }

        return Ok(_mapper.Map<(string Status, bool UserExists), PublicInvitationStatusDto>((invitation.Status.ToString(), userExists)));
    }
}
