using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.User;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace LgymApi.Api.Features.User.Controllers;

[ApiController]
[Route("api")]
public sealed class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        await _userService.RegisterAsync(request.Name, request.Email, request.Password, request.ConfirmPassword, request.IsVisibleInRanking);

        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _userService.LoginAsync(request.Name, request.Password);
        return Ok(new LoginResponseDto
        {
            Token = result.Token,
            PermissionClaims = result.PermissionClaims,
            User = new UserInfoDto
            {
                Name = result.User.Name,
                Id = result.User.Id.ToString(),
                Email = result.User.Email,
                Avatar = result.User.Avatar,
                ProfileRank = result.User.ProfileRank,
                CreatedAt = result.User.CreatedAt,
                UpdatedAt = result.User.UpdatedAt,
                Elo = result.User.Elo,
                NextRank = result.User.NextRank == null ? null : new RankDto { Name = result.User.NextRank.Name, NeedElo = result.User.NextRank.NeedElo },
                IsDeleted = result.User.IsDeleted,
                IsVisibleInRanking = result.User.IsVisibleInRanking,
                Roles = result.User.Roles,
                PermissionClaims = result.User.PermissionClaims
            }
        });
    }

    [HttpGet("{id}/isAdmin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> IsAdmin([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var result = await _userService.IsAdminAsync(userId);
        return Ok(result);
    }

    [HttpGet("checkToken")]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckToken()
    {
        var user = HttpContext.GetCurrentUser();
        var result = await _userService.CheckTokenAsync(user!);
        return Ok(new UserInfoDto
        {
            Name = result.Name,
            Id = result.Id.ToString(),
            Email = result.Email,
            Avatar = result.Avatar,
            ProfileRank = result.ProfileRank,
            CreatedAt = result.CreatedAt,
            UpdatedAt = result.UpdatedAt,
            Elo = result.Elo,
            NextRank = result.NextRank == null ? null : new RankDto { Name = result.NextRank.Name, NeedElo = result.NextRank.NeedElo },
            IsDeleted = result.IsDeleted,
            IsVisibleInRanking = result.IsVisibleInRanking,
            Roles = result.Roles,
            PermissionClaims = result.PermissionClaims
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var user = HttpContext.GetCurrentUser();
        await _userService.LogoutAsync(user!);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpGet("getUsersRanking")]
    [ProducesResponseType(typeof(List<UserBaseInfoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsersRanking()
    {
        var result = await _userService.GetUsersRankingAsync();
        var mapped = result.Select(u => new UserBaseInfoDto
        {
            Name = u.Name,
            Avatar = u.Avatar,
            Elo = u.Elo,
            ProfileRank = u.ProfileRank
        }).ToList();
        return Ok(mapped);
    }

    [HttpGet("userInfo/{id}/getUserEloPoints")]
    [ProducesResponseType(typeof(UserEloDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserElo([FromRoute] string id)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var elo = await _userService.GetUserEloAsync(userId);
        return Ok(new UserEloDto { Elo = elo });
    }

    [HttpGet("deleteAccount")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = HttpContext.GetCurrentUser();
        await _userService.DeleteAccountAsync(user!);
        return Ok(new ResponseMessageDto { Message = Messages.Deleted });
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
        await _userService.ChangeVisibilityInRankingAsync(user!, isVisible);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }

    [HttpPut("users/{id}/roles")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserRoles([FromRoute] string id, [FromBody] UpdateUserRolesRequest request)
    {
        var userId = Guid.TryParse(id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        await _userService.UpdateUserRolesAsync(userId, request.Roles);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }
}
