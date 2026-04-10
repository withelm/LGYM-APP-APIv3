using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.PasswordReset;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserSessionEntity = LgymApi.Domain.Entities.UserSession;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Api.Features.User.Controllers;

[ApiController]
[Route("api")]
public sealed class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IMapper _mapper;

    public UserController(IUserService userService, IPasswordResetService passwordResetService, IMapper mapper)
    {
        _userService = userService;
        _passwordResetService = passwordResetService;
        _mapper = mapper;
    }

    [HttpPost("register")]
    [ApiIdempotency("/api/register", ApiIdempotencyScopeSource.NormalizedEmail)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        var preferredLanguage = Request.Headers.TryGetValue("Accept-Language", out var langs)
            ? langs.ToString()
            : null;

        var input = new RegisterUserInput(
            request.Name,
            request.Email,
            request.Password,
            request.ConfirmPassword,
            request.IsVisibleInRanking,
            preferredLanguage);

        var result = await _userService.RegisterAsync(input, cancellationToken);
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
        var result = await _userService.LoginAsync(request.Name, request.Password, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mapped = _mapper.Map<LoginResult, LoginResponseDto>(result.Value);
        return Ok(mapped);
    }

    [HttpGet("{id}/isAdmin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> IsAdmin([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var userId = ParseUserId(id);
        var result = await _userService.IsAdminAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("checkToken")]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckToken(CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _userService.CheckTokenAsync(user, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mapped = _mapper.Map<UserInfoResult, UserInfoDto>(result.Value);
        return Ok(mapped);
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        var rawSessionId = HttpContext.User.FindFirst("sid")?.Value;
        var sessionId = Id<UserSessionEntity>.TryParse(rawSessionId, out var parsedSessionId)
            ? parsedSessionId
            : (Id<UserSessionEntity>?)null;

        var result = await _userService.LogoutAsync(user, sessionId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("getUsersRanking")]
    [ProducesResponseType(typeof(List<UserBaseInfoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsersRanking(CancellationToken cancellationToken = default)
    {
        var result = await _userService.GetUsersRankingAsync(cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        var mapped = _mapper.MapList<RankingEntry, UserBaseInfoDto>(result.Value);
        return Ok(mapped);
    }

    [HttpGet("userInfo/{id}/getUserEloPoints")]
    [ProducesResponseType(typeof(UserEloDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserElo([FromRoute] string id, CancellationToken cancellationToken = default)
    {
        var userId = ParseUserId(id);
        var result = await _userService.GetUserEloAsync(userId, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<int, UserEloDto>(result.Value));
    }

    private static Id<UserEntity> ParseUserId(string value)
    {
        return Id<UserEntity>.TryParse(value, out var parsedUserId)
            ? parsedUserId
            : Id<UserEntity>.Empty;
    }

    [HttpGet("deleteAccount")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _userService.DeleteAccountAsync(user, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("changeVisibilityInRanking")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeVisibilityInRanking([FromBody] Dictionary<string, bool> body, CancellationToken cancellationToken = default)
    {
        if (!body.TryGetValue("isVisibleInRanking", out var isVisible))
        {
            var routeValidationFailure = Result<Unit, AppError>.Failure(new InvalidUserError(Messages.DidntFind));
            return routeValidationFailure.ToActionResult();
        }

        var user = HttpContext.GetCurrentUser();
        var result = await _userService.ChangeVisibilityInRankingAsync(user, isVisible, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("updateTimeZone")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTimeZone([FromBody] UpdateTimeZoneRequest request, CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _userService.UpdateTimeZoneAsync(user, request.PreferredTimeZone, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

     [HttpPost("forgot-password")]
     [AllowAnonymous]
     [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
     public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken = default)
     {
         var preferences = HttpContext.GetCulturePreferences();
         await _passwordResetService.RequestPasswordResetAsync(request.Email, preferences.First(), cancellationToken);
         return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.ForgotPasswordRequested));
     }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _passwordResetService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(_mapper.Map<string, ResponseMessageDto>(Messages.PasswordResetInvalidOrExpiredToken));
        }

        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.PasswordResetSucceeded));
    }
}
