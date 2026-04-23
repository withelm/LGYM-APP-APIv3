using System.Text.Json.Serialization;
using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Account.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Application.ExternalAuth;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Account.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountLinkingService _accountLinkingService;
    private readonly IMapper _mapper;

    public AccountController(IAccountLinkingService accountLinkingService, IMapper mapper)
    {
        _accountLinkingService = accountLinkingService;
        _mapper = mapper;
    }

    [HttpPost("link-google")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkGoogle([FromBody] LinkGoogleRequest request, CancellationToken cancellationToken = default)
    {
        // validation (FluentValidation will run automatically when configured)
        var userId = HttpContext.GetCurrentUserId();

        var result = await _accountLinkingService.LinkGoogleAsync(userId, request.IdToken, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(LgymApi.Resources.Messages.Updated));
    }

    [HttpGet("external-logins")]
    [ProducesResponseType(typeof(ExternalLoginDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExternalLogins(CancellationToken cancellationToken = default)
    {
        var userId = HttpContext.GetCurrentUserId();
        var result = await _accountLinkingService.GetExternalLoginsAsync(userId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mapped = _mapper.MapList<LgymApi.Application.ExternalAuth.ExternalLoginInfo, ExternalLoginDto>(result.Value);
        return Ok(mapped);
    }
}
