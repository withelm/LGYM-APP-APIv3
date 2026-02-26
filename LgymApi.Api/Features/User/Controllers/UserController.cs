using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Mapping.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.User.Controllers;

[ApiController]
[Route("api")]
public sealed class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public UserController(IUserService userService, IMapper mapper)
    {
        _userService = userService;
        _mapper = mapper;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        var preferredLanguage = "en-US";
        if (Request.Headers.TryGetValue("Accept-Language", out var langs))
        {
            preferredLanguage = langs
                .ToString()
                .Split(',')
                .FirstOrDefault()?
                .Split(';')
                .FirstOrDefault()?
                .Trim();
        }
        await _userService.RegisterAsync(
            request.Name,
            request.Email,
            request.Password,
            request.ConfirmPassword,
            request.IsVisibleInRanking,
            preferredLanguage,
            HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Created));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _userService.LoginAsync(request.Name, request.Password, HttpContext.RequestAborted);
        var mapped = _mapper.Map<LoginResult, LoginResponseDto>(result);
        return Ok(mapped);
    }

    [HttpGet("{id}/isAdmin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> IsAdmin([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var result = await _userService.IsAdminAsync(userId, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("checkToken")]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckToken()
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _userService.CheckTokenAsync(user!, HttpContext.RequestAborted);
        var mapped = _mapper.Map<UserInfoResult, UserInfoDto>(result);
        return Ok(mapped);
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var user = HttpContext.GetCurrentUser();
        await _userService.LogoutAsync(user!, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpGet("getUsersRanking")]
    [ProducesResponseType(typeof(List<UserBaseInfoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsersRanking()
    {
        var result = await _userService.GetUsersRankingAsync(HttpContext.RequestAborted);
        var mapped = _mapper.MapList<RankingEntry, UserBaseInfoDto>(result);
        return Ok(mapped);
    }

    [HttpGet("userInfo/{id}/getUserEloPoints")]
    [ProducesResponseType(typeof(UserEloDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserElo([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var elo = await _userService.GetUserEloAsync(userId, HttpContext.RequestAborted);
        return Ok(_mapper.Map<int, UserEloDto>(elo));
    }

    [HttpGet("deleteAccount")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = HttpContext.GetCurrentUser();
        await _userService.DeleteAccountAsync(user!, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Deleted));
    }

    [HttpPost("changeVisibilityInRanking")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeVisibilityInRanking([FromBody] Dictionary<string, bool> body)
    {
        if (!body.TryGetValue("isVisibleInRanking", out var isVisible))
        {
            throw AppException.BadRequest(Messages.DidntFind);
        }

        var user = HttpContext.GetCurrentUser();
        await _userService.ChangeVisibilityInRankingAsync(user!, isVisible, HttpContext.RequestAborted);
        return Ok(_mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }
}
