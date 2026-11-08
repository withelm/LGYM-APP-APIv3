using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Application.Identity.Contracts.Authentication;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Security;
using LgymApi.Identity.Contracts.Approval;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainer")]
public sealed class TrainerAuthController : ControllerBase
{
    private readonly IUserCredentialLoginService _userCredentialLoginService;
    private readonly IAuthenticatedAccountApiAdapter _authenticatedAccountApiAdapter;
    private readonly IAccountEloApiAdapter _accountEloApiAdapter;
    private readonly IEloRegistryService _eloRegistryService;
    private readonly IMapper _mapper;
    private readonly ITrainerApprovalService _trainerApproval;

    public TrainerAuthController(
        IUserCredentialLoginService userCredentialLoginService,
        IAuthenticatedAccountApiAdapter authenticatedAccountApiAdapter,
        IAccountEloApiAdapter accountEloApiAdapter,
        IEloRegistryService eloRegistryService,
        IMapper mapper,
        ITrainerApprovalService trainerApproval)
    {
        _userCredentialLoginService = userCredentialLoginService;
        _authenticatedAccountApiAdapter = authenticatedAccountApiAdapter;
        _accountEloApiAdapter = accountEloApiAdapter;
        _eloRegistryService = eloRegistryService;
        _mapper = mapper;
        _trainerApproval = trainerApproval;
    }

    [HttpPost("register")]
    [ApiIdempotency("/api/trainer/register", ApiIdempotencyScopeSource.NormalizedEmail, ApiIdempotencyReplayOwner.Public)]
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

        var result = await _eloRegistryService.RegisterUserAsync(input, trainer: true, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.TrainerPendingApproval));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TrainerApprovalErrorDto), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _userCredentialLoginService.LoginTrainerAsync(request.Name, request.Password, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        await _eloRegistryService.PopulateLatestEloAsync(result.Value.User, cancellationToken);
        return Ok(_mapper.Map<LgymApi.Application.Features.User.Models.LoginResult, LoginResponseDto>(result.Value));
    }

    [HttpPost("application/resubmit")]
    [Authorize]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(TrainerApprovalErrorDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResubmitApplication(CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (HttpContext.GetAuthenticatedAccountContext() is not { } actor) return Unauthorized();
        var result = await _trainerApproval.ResubmitAsync(actor, cancellationToken);
        return result.IsFailure ? result.ToActionResult()
            : Ok(_mapper.Map<string, ResponseMessageDto>(Messages.TrainerPendingApproval));
    }

    [HttpGet("checkToken")]
    [Authorize(Policy = AuthConstants.Policies.TrainerAccess)]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckToken(CancellationToken cancellationToken = default)
    {
        var result = await _authenticatedAccountApiAdapter.CheckTokenAsync(HttpContext.GetCurrentAccountId(), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var account = await _accountEloApiAdapter.PopulateLatestEloAsync(result.Value, cancellationToken);
        return Ok(_mapper.Map<AccountProfileProjection, UserInfoDto>(account));
    }
}
