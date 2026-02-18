using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.User;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[Route("api/trainer")]
public sealed class TrainerAuthController : ControllerBase
{
    private readonly IUserService _userService;

    public TrainerAuthController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        await _userService.RegisterTrainerAsync(request.Name, request.Email, request.Password, request.ConfirmPassword);
        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _userService.LoginTrainerAsync(request.Name, request.Password);
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

    [HttpGet("checkToken")]
    [Authorize(Policy = AuthConstants.Policies.TrainerAccess)]
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
}
