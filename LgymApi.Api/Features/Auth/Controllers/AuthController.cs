using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Auth.Contracts;
using LgymApi.Application.ExternalAuth;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IExternalAuthService _externalAuthService;
    private readonly IMapper _mapper;

    public AuthController(IExternalAuthService externalAuthService, IMapper mapper)
    {
        _externalAuthService = externalAuthService;
        _mapper = mapper;
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LgymApi.Api.Features.User.Contracts.LoginResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Google([FromBody] GoogleSignInRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _externalAuthService.GoogleSignInAsync(request.IdToken, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mapped = _mapper.Map<LgymApi.Application.Features.User.Models.LoginResult, LgymApi.Api.Features.User.Contracts.LoginResponseDto>(result.Value);
        return Ok(mapped);
    }
}
