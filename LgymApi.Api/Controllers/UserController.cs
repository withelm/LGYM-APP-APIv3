using System.ComponentModel.DataAnnotations;
using LgymApi.Api.DTOs;
using LgymApi.Api.Middleware;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IEloRegistryRepository _eloRepository;
    private readonly ITokenService _tokenService;
    private readonly ILegacyPasswordService _legacyPasswordService;
    private readonly IRankService _rankService;

    public UserController(
        IUserRepository userRepository,
        IEloRegistryRepository eloRepository,
        ITokenService tokenService,
        ILegacyPasswordService legacyPasswordService,
        IRankService rankService)
    {
        _userRepository = userRepository;
        _eloRepository = eloRepository;
        _tokenService = tokenService;
        _legacyPasswordService = legacyPasswordService;
        _rankService = rankService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.NameIsRequired });
        }

        if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.EmailInvalid });
        }

        if (request.Password.Length < 6)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.PasswordMin });
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.SamePassword });
        }

        var existingUser = await _userRepository.FindByNameOrEmailAsync(request.Name, request.Email);

        if (existingUser != null)
        {
            if (string.Equals(existingUser.Name, request.Name, StringComparison.Ordinal))
            {
                return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.UserWithThatName });
            }

            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.UserWithThatEmail });
        }

        var passwordData = _legacyPasswordService.Create(request.Password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Admin = false,
            Email = request.Email,
            IsVisibleInRanking = request.IsVisibleInRanking ?? true,
            ProfileRank = "Junior 1",
            LegacyHash = passwordData.Hash,
            LegacySalt = passwordData.Salt,
            LegacyIterations = passwordData.Iterations,
            LegacyKeyLength = passwordData.KeyLength,
            LegacyDigest = passwordData.Digest
        };

        await _userRepository.AddAsync(user);
        await _eloRepository.AddAsync(new EloRegistry
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Date = DateTimeOffset.UtcNow,
            Elo = 1000
        });

        return Ok(new ResponseMessageDto { Message = Messages.Created });
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Password))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new ResponseMessageDto { Message = Messages.Unauthorized });
        }

        var user = await _userRepository.FindByNameAsync(request.Name);
        if (user == null || string.IsNullOrWhiteSpace(user.LegacyHash) || string.IsNullOrWhiteSpace(user.LegacySalt))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new ResponseMessageDto { Message = Messages.Unauthorized });
        }

        var valid = _legacyPasswordService.Verify(
            request.Password,
            user.LegacyHash,
            user.LegacySalt,
            user.LegacyIterations,
            user.LegacyKeyLength,
            user.LegacyDigest);

        if (!valid)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new ResponseMessageDto { Message = Messages.Unauthorized });
        }

        var token = _tokenService.CreateToken(user.Id);
        var elo = await _eloRepository.GetLatestEloAsync(user.Id) ?? 1000;

        var nextRank = _rankService.GetNextRank(user.ProfileRank);
        var response = new LoginResponseDto
        {
            Token = token,
            User = new UserInfoDto
            {
                Name = user.Name,
                Id = user.Id.ToString(),
                Email = user.Email,
                Avatar = user.Avatar,
                Admin = user.Admin,
                ProfileRank = user.ProfileRank,
                CreatedAt = user.CreatedAt.UtcDateTime,
                UpdatedAt = user.UpdatedAt.UtcDateTime,
                Elo = elo,
                NextRank = nextRank == null ? null : new RankDto { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
                IsDeleted = user.IsDeleted,
                IsTester = user.IsTester,
                IsVisibleInRanking = user.IsVisibleInRanking
            }
        };

        return Ok(response);
    }

    [HttpGet("{id}/isAdmin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> IsAdmin([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return Ok(false);
        }

        var user = await _userRepository.FindByIdAsync(userId);
        var result = user != null && user.Admin == true;
        return Ok(result);
    }

    [HttpGet("checkToken")]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckToken()
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var nextRank = _rankService.GetNextRank(user.ProfileRank);
        var elo = await _eloRepository.GetLatestEloAsync(user.Id) ?? 1000;

        var response = new UserInfoDto
        {
            Name = user.Name,
            Id = user.Id.ToString(),
            Email = user.Email,
            Avatar = user.Avatar,
            Admin = user.Admin,
            ProfileRank = user.ProfileRank,
            CreatedAt = user.CreatedAt.UtcDateTime,
            UpdatedAt = user.UpdatedAt.UtcDateTime,
            Elo = elo,
            NextRank = nextRank == null ? null : new RankDto { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
            IsDeleted = user.IsDeleted,
            IsTester = user.IsTester,
            IsVisibleInRanking = user.IsVisibleInRanking
        };

        return Ok(response);
    }

    [HttpGet("getUsersRanking")]
    [ProducesResponseType(typeof(List<UserBaseInfoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsersRanking()
    {
        var users = await _userRepository.GetRankingAsync();

        if (users.Count == 0)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var result = users.Select(u => new UserBaseInfoDto
        {
            Name = u.User.Name,
            Avatar = u.User.Avatar,
            Elo = u.Elo,
            ProfileRank = u.User.ProfileRank
        }).ToList();

        return Ok(result);
    }

    [HttpGet("userInfo/{id}/getUserEloPoints")]
    [ProducesResponseType(typeof(UserEloDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserElo([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var result = await _eloRepository.GetLatestEloAsync(userId);

        if (!result.HasValue)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        return Ok(new UserEloDto { Elo = result.Value });
    }

    [HttpGet("deleteAccount")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status404NotFound, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        user.Email = $"anonymized_{user.Id}@example.com";
        user.Name = $"anonymized_user_{user.Id}";
        user.IsDeleted = true;

        await _userRepository.UpdateAsync(user);
        return Ok(new ResponseMessageDto { Message = Messages.Deleted });
    }

    [HttpPost("changeVisibilityInRanking")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeVisibilityInRanking([FromBody] Dictionary<string, bool> body)
    {
        if (!body.TryGetValue("isVisibleInRanking", out var isVisible))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        var user = HttpContext.GetCurrentUser();
        if (user == null)
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ResponseMessageDto { Message = Messages.DidntFind });
        }

        user.IsVisibleInRanking = isVisible;
        await _userRepository.UpdateAsync(user);
        return Ok(new ResponseMessageDto { Message = Messages.Updated });
    }
}
