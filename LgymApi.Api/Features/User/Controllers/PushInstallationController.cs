using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using UserEntity = LgymApi.Domain.Entities.User;
using UserSessionEntity = LgymApi.Domain.Entities.UserSession;

namespace LgymApi.Api.Features.User.Controllers;

[ApiController]
[Route("api/push/installations")]
public sealed class PushInstallationController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public PushInstallationController(IUserService userService, IMapper mapper)
    {
        _userService = userService;
        _mapper = mapper;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterPushInstallationRequest request, CancellationToken cancellationToken = default)
    {
        var input = new RegisterPushInstallationInput(
            request.InstallationId,
            request.Platform,
            request.FcmToken,
            request.AppVersion,
            request.Environment,
            request.PermissionStatus);

        var result = await _userService.RegisterPushInstallationAsync(
            HttpContext.GetCurrentUser(),
            ParseCurrentSessionId(),
            input,
            cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("unregister")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unregister([FromBody] PushInstallationActionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.UnregisterPushInstallationAsync(
            HttpContext.GetCurrentUser(),
            ParseCurrentSessionId(),
            new PushInstallationActionInput(request.InstallationId),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("disassociate")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disassociate([FromBody] PushInstallationActionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.DisassociatePushInstallationAsync(
            HttpContext.GetCurrentUser(),
            ParseCurrentSessionId(),
            new PushInstallationActionInput(request.InstallationId),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    private Id<UserSessionEntity>? ParseCurrentSessionId()
    {
        var rawSessionId = HttpContext.User.FindFirst(AuthConstants.ClaimNames.SessionId)?.Value;
        if (string.IsNullOrWhiteSpace(rawSessionId))
        {
            return null;
        }

        return Id<UserSessionEntity>.TryParse(rawSessionId, out var parsedSessionId)
            ? parsedSessionId
            : null;
    }
}
