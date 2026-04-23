using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Auth.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.ExternalAuth;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Resources;
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
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Google([FromBody] GoogleSignInRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _externalAuthService.GoogleSignInAsync(request.IdToken, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mapped = _mapper.Map<LoginResult, LoginResponseDto>(result.Value);
        return Ok(mapped);
    }
}
