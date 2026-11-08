using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Identity.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using LgymApi.Identity.Approval;

namespace LgymApi.Application.ExternalAuth;

internal sealed class LoginResultBuilder : ILoginResultBuilder
{
    private readonly IRankService _rankService;
    private readonly ITokenService _tokenService;
    private readonly ITutorialService _tutorialService;
    private readonly IUserRepository _userRepository;
    private readonly IUserSessionStore _userSessionStore;
    private readonly IMapper _mapper;
    private readonly ITrainerApplicationRepository _trainerApplications;

    public LoginResultBuilder(
        IUserRepository userRepository,
        IUserSessionStore userSessionStore,
        ITokenService tokenService,
        IRankService rankService,
        ITutorialService tutorialService,
        IMapper mapper,
        ITrainerApplicationRepository trainerApplications)
    {
        _userRepository = userRepository;
        _userSessionStore = userSessionStore;
        _tokenService = tokenService;
        _rankService = rankService;
        _tutorialService = tutorialService;
        _mapper = mapper;
        _trainerApplications = trainerApplications;
    }

    public async Task<Result<LoginResult, AppError>> BuildAsync(User user, string preferredTimeZone, CancellationToken cancellationToken)
    {
        var userWithRoles = await _userRepository.FindByIdWithRolesAsync(user.Id, cancellationToken);
        if (userWithRoles == null)
        {
            return Result<LoginResult, AppError>.Failure(new InternalServerError(Messages.UserLoadFailed));
        }

        var roles = userWithRoles.UserRoles.Select(ur => ur.Role.Name).OrderBy(name => name).ToList();
        var permissionClaims = userWithRoles.UserRoles
            .SelectMany(ur => ur.Role.RoleClaims)
            .Where(rc => rc.ClaimType == LgymApi.Domain.Security.AuthConstants.PermissionClaimType)
            .Select(rc => rc.ClaimValue)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        var approvalStatus = await _trainerApplications.GetLatestStatusAsync(user.Id, cancellationToken);
        var isApproved = TrainerApprovalPolicy.IsApproved(userWithRoles, approvalStatus);
        roles = TrainerApprovalPolicy.EffectiveRoles(roles, isApproved);
        permissionClaims = TrainerApprovalPolicy.EffectivePermissions(permissionClaims, isApproved);

        var session = await _userSessionStore.CreateSessionAsync(userWithRoles.Id, DateTimeOffset.UtcNow.AddDays(30), cancellationToken);

        var token = _tokenService.CreateToken(userWithRoles.Id, session.Id, session.Jti, roles, permissionClaims);
        var nextRank = _rankService.GetNextRank(userWithRoles.ProfileRank);
        var hasActiveTutorials = await _tutorialService.HasActiveTutorialsAsync(userWithRoles.Id, cancellationToken);
        var context = _mapper.CreateContext();
        context.Set(IdentityUserMappingProfile.Keys.DefaultPreferredTimeZone, preferredTimeZone);
        context.Set(IdentityUserMappingProfile.Keys.NextRank, nextRank);
        context.Set(IdentityUserMappingProfile.Keys.Roles, roles);
        context.Set(IdentityUserMappingProfile.Keys.PermissionClaims, permissionClaims);
        context.Set(IdentityUserMappingProfile.Keys.HasActiveTutorials, hasActiveTutorials);
        context.Set(IdentityUserMappingProfile.Keys.TrainerApprovalStatus, approvalStatus);

        return Result<LoginResult, AppError>.Success(new LoginResult
        {
            Token = token,
            PermissionClaims = permissionClaims,
            User = _mapper.Map<User, UserInfoResult>(userWithRoles, context)
        });
    }
}
