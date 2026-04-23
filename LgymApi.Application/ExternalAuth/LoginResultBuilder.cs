using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.ExternalAuth;

public sealed class LoginResultBuilder : ILoginResultBuilder
{
    private readonly IEloRegistryRepository _eloRepository;
    private readonly IRankService _rankService;
    private readonly ITokenService _tokenService;
    private readonly ITutorialService _tutorialService;
    private readonly IUserSessionStore _userSessionStore;

    public LoginResultBuilder(
        IUserSessionStore userSessionStore,
        ITokenService tokenService,
        IEloRegistryRepository eloRepository,
        IRankService rankService,
        ITutorialService tutorialService)
    {
        _userSessionStore = userSessionStore;
        _tokenService = tokenService;
        _eloRepository = eloRepository;
        _rankService = rankService;
        _tutorialService = tutorialService;
    }

    public async Task<Result<LoginResult, AppError>> BuildAsync(User user, string preferredTimeZone, CancellationToken cancellationToken)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).OrderBy(name => name).ToList();
        var permissionClaims = user.UserRoles
            .SelectMany(ur => ur.Role.RoleClaims)
            .Where(rc => rc.ClaimType == LgymApi.Domain.Security.AuthConstants.PermissionClaimType)
            .Select(rc => rc.ClaimValue)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        var session = await _userSessionStore.CreateSessionAsync(user.Id, DateTimeOffset.UtcNow.AddDays(30), cancellationToken);

        var token = _tokenService.CreateToken(user.Id, session.Id, session.Jti, roles, permissionClaims);
        var elo = await _eloRepository.GetLatestEloAsync(user.Id, cancellationToken) ?? 1000;
        var nextRank = _rankService.GetNextRank(user.ProfileRank);
        var hasActiveTutorials = await _tutorialService.HasActiveTutorialsAsync(user.Id, cancellationToken);

        return Result<LoginResult, AppError>.Success(new LoginResult
        {
            Token = token,
            PermissionClaims = permissionClaims,
            User = new UserInfoResult
            {
                Name = user.Name,
                Id = user.Id,
                Email = user.Email,
                Avatar = user.Avatar,
                ProfileRank = user.ProfileRank,
                PreferredTimeZone = string.IsNullOrWhiteSpace(user.PreferredTimeZone) ? preferredTimeZone : user.PreferredTimeZone,
                CreatedAt = user.CreatedAt.UtcDateTime,
                UpdatedAt = user.UpdatedAt.UtcDateTime,
                Elo = elo,
                NextRank = nextRank == null ? null : new RankInfo { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
                IsDeleted = user.IsDeleted,
                IsVisibleInRanking = user.IsVisibleInRanking,
                Roles = roles,
                PermissionClaims = permissionClaims,
                HasActiveTutorials = hasActiveTutorials
            }
        });
    }
}
