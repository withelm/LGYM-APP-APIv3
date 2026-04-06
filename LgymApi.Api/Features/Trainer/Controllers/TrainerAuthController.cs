using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainer")]
public sealed class TrainerAuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public TrainerAuthController(IUserService userService, IMapper mapper)
    {
        _userService = userService;
        _mapper = mapper;
    }

    [HttpPost("register")]
    [ApiIdempotency("/api/trainer/register", ApiIdempotencyScopeSource.NormalizedEmail)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        var input = new RegisterUserInput(
            request.Name,
            request.Email,
            request.Password,
            request.ConfirmPassword,
            IsVisibleInRanking: null,
            PreferredLanguage: null);

        var result = await _userService.RegisterTrainerAsync(input, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.LoginTrainerAsync(request.Name, request.Password, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<LgymApi.Application.Features.User.Models.LoginResult, LoginResponseDto>(result.Value));
    }

    [HttpGet("checkToken")]
    [Authorize(Policy = AuthConstants.Policies.TrainerAccess)]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckToken(CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _userService.CheckTokenAsync(user, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<LgymApi.Application.Features.User.Models.UserInfoResult, UserInfoDto>(result.Value));
    }
}
