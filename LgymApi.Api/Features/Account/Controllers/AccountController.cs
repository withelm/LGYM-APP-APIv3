using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Account.Contracts;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Account.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountExternalLoginApiAdapter _accountExternalLoginApiAdapter;
    private readonly IAuthenticatedAccountAccessCapabilitiesService _accountAccessCapabilitiesService;
    private readonly IMapper _mapper;

    public AccountController(
        IAccountExternalLoginApiAdapter accountExternalLoginApiAdapter,
        IAuthenticatedAccountAccessCapabilitiesService accountAccessCapabilitiesService,
        IMapper mapper)
    {
        _accountExternalLoginApiAdapter = accountExternalLoginApiAdapter;
        _accountAccessCapabilitiesService = accountAccessCapabilitiesService;
        _mapper = mapper;
    }

    /// <summary>Returns schema-versioned capabilities for the authenticated account only.</summary>
    [HttpGet("access")]
    [Tags("Authorization")]
    [ProducesResponseType(typeof(AccountAccessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status401Unauthorized)]
    public IActionResult GetAccess()
    {
        var accountContext = HttpContext.GetAuthenticatedAccountContext();
        if (accountContext is null)
        {
            return Unauthorized();
        }

        Response.Headers.CacheControl = "private, no-store";
        var capabilities = _accountAccessCapabilitiesService.GetCapabilities(accountContext);
        return Ok(_mapper.Map<AccountAccessCapabilities, AccountAccessResponseDto>(capabilities));
    }

    [HttpPost("link-google")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkGoogle([FromBody] LinkGoogleRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _accountExternalLoginApiAdapter.LinkGoogleAsync(GetCurrentAccountId(), request.IdToken, request.AccessToken, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(LgymApi.Resources.Messages.Updated));
    }

    [HttpPost("unlink-google")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnlinkGoogle(CancellationToken cancellationToken = default)
    {
        var result = await _accountExternalLoginApiAdapter.UnlinkGoogleAsync(GetCurrentAccountId(), cancellationToken);
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
        var result = await _accountExternalLoginApiAdapter.GetExternalLoginsAsync(GetCurrentAccountId(), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mapped = _mapper.MapList<ExternalLoginProjection, ExternalLoginDto>(result.Value);
        return Ok(mapped);
    }

    private Id<AccountReference> GetCurrentAccountId()
        => HttpContext.GetAuthenticatedAccountContext()?.Id ?? Id<AccountReference>.Empty;
}
