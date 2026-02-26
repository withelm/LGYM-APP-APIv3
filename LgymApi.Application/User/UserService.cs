using System.ComponentModel.DataAnnotations;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Application.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.User;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IEloRegistryRepository _eloRepository;
    private readonly ITokenService _tokenService;
    private readonly ILegacyPasswordService _legacyPasswordService;
    private readonly IRankService _rankService;
    private readonly IUserSessionCache _userSessionCache;
    private readonly IEmailScheduler<LgymApi.Application.Notifications.Models.WelcomeEmailPayload> _welcomeEmailScheduler;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "User service coordinates auth, ranking, roles, and session concerns.")]
    public UserService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IEloRegistryRepository eloRepository,
        ITokenService tokenService,
        ILegacyPasswordService legacyPasswordService,
        IRankService rankService,
        IUserSessionCache userSessionCache,
        IEmailScheduler<LgymApi.Application.Notifications.Models.WelcomeEmailPayload> welcomeEmailScheduler,
        IUnitOfWork unitOfWork,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _eloRepository = eloRepository;
        _tokenService = tokenService;
        _legacyPasswordService = legacyPasswordService;
        _rankService = rankService;
        _userSessionCache = userSessionCache;
        _welcomeEmailScheduler = welcomeEmailScheduler;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task RegisterAsync(string name, string email, string password, string confirmPassword, bool? isVisibleInRanking, string? preferredLanguage = null, CancellationToken cancellationToken = default)
    {
        await RegisterCoreAsync(
            name,
            email,
            password,
            confirmPassword,
            isVisibleInRanking,
            [AuthConstants.Roles.User],
            preferredLanguage,
            cancellationToken);
    }

    public async Task RegisterTrainerAsync(string name, string email, string password, string confirmPassword, CancellationToken cancellationToken = default)
    {
        await RegisterCoreAsync(
            name,
            email,
            password,
            confirmPassword,
            isVisibleInRanking: false,
            [AuthConstants.Roles.User, AuthConstants.Roles.Trainer],
            preferredLanguage: null,
            cancellationToken);
    }

    public async Task<LoginResult> LoginAsync(string name, string password, CancellationToken cancellationToken = default)
    {
        return await LoginCoreAsync(name, password, requiredRole: null, cancellationToken);
    }

    public async Task<LoginResult> LoginTrainerAsync(string name, string password, CancellationToken cancellationToken = default)
    {
        return await LoginCoreAsync(name, password, AuthConstants.Roles.Trainer, cancellationToken);
    }

    private async Task RegisterCoreAsync(
        string name,
        string email,
        string password,
        string confirmPassword,
        bool? isVisibleInRanking,
        IReadOnlyCollection<string> roleNames,
        string? preferredLanguage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.NotFound(Messages.NameIsRequired);
        }

        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (!new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            throw AppException.NotFound(Messages.EmailInvalid);
        }

        if (password.Length < 6)
        {
            throw AppException.NotFound(Messages.PasswordMin);
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            throw AppException.NotFound(Messages.SamePassword);
        }

        var existingUser = await _userRepository.FindByNameOrEmailAsync(name, normalizedEmail!, cancellationToken);
        if (existingUser != null)
        {
            if (string.Equals(existingUser.Name, name, StringComparison.Ordinal))
            {
                throw AppException.NotFound(Messages.UserWithThatName);
            }

            throw AppException.NotFound(Messages.UserWithThatEmail);
        }

        var passwordData = _legacyPasswordService.Create(password);
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = normalizedEmail!,
            IsVisibleInRanking = isVisibleInRanking ?? true,
            ProfileRank = "Junior 1",
            LegacyHash = passwordData.Hash,
            LegacySalt = passwordData.Salt,
            LegacyIterations = passwordData.Iterations,
            LegacyKeyLength = passwordData.KeyLength,
            LegacyDigest = passwordData.Digest,
            PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? "en-US" : preferredLanguage
        };

        await _userRepository.AddAsync(user, cancellationToken);

        var rolesToAssign = await _roleRepository.GetByNamesAsync(roleNames, cancellationToken);
        if (rolesToAssign.Count != roleNames.Count)
        {
            throw AppException.Internal(Messages.DefaultRoleMissing);
        }

        await _roleRepository.AddUserRolesAsync(user.Id, rolesToAssign.Select(r => r.Id).ToList(), cancellationToken);

        await _eloRepository.AddAsync(new global::LgymApi.Domain.Entities.EloRegistry
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Date = DateTimeOffset.UtcNow,
            Elo = 1000
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await TryScheduleWelcomeEmailAsync(user, cancellationToken);
    }

    private async Task TryScheduleWelcomeEmailAsync(UserEntity user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogInformation(
                "User email is empty; welcome email will not be scheduled for user {UserId}.",
                user.Id);
            return;
        }

        try
        {
            await _welcomeEmailScheduler.ScheduleAsync(new LgymApi.Application.Notifications.Models.WelcomeEmailPayload
            {
                UserId = user.Id,
                UserName = user.Name,
                RecipientEmail = user.Email,
                CultureName = string.IsNullOrWhiteSpace(user.PreferredLanguage) ? "en-US" : user.PreferredLanguage
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to schedule welcome email for user {UserId}. Registration is still successful.",
                user.Id);
        }
    }

    private async Task<LoginResult> LoginCoreAsync(string name, string password, string? requiredRole, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
        {
            throw AppException.Unauthorized(Messages.Unauthorized);
        }

        var user = await _userRepository.FindByNameAsync(name, cancellationToken);
        if (user == null || string.IsNullOrWhiteSpace(user.LegacyHash) || string.IsNullOrWhiteSpace(user.LegacySalt))
        {
            throw AppException.Unauthorized(Messages.Unauthorized);
        }

        var valid = _legacyPasswordService.Verify(
            password,
            user.LegacyHash,
            user.LegacySalt,
            user.LegacyIterations,
            user.LegacyKeyLength,
            user.LegacyDigest);

        if (!valid)
        {
            throw AppException.Unauthorized(Messages.Unauthorized);
        }

        var roles = await _roleRepository.GetRoleNamesByUserIdAsync(user.Id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(requiredRole) &&
            !roles.Contains(requiredRole, StringComparer.Ordinal))
        {
            throw AppException.Unauthorized(Messages.Unauthorized);
        }

        var permissionClaims = await _roleRepository.GetPermissionClaimsByUserIdAsync(user.Id, cancellationToken);
        var token = _tokenService.CreateToken(user.Id, roles, permissionClaims);
        var elo = await _eloRepository.GetLatestEloAsync(user.Id, cancellationToken) ?? 1000;
        var nextRank = _rankService.GetNextRank(user.ProfileRank);

        _userSessionCache.AddOrRefresh(user.Id);

        return new LoginResult
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
                CreatedAt = user.CreatedAt.UtcDateTime,
                UpdatedAt = user.UpdatedAt.UtcDateTime,
                Elo = elo,
                NextRank = nextRank == null ? null : new RankInfo { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
                IsDeleted = user.IsDeleted,
                IsVisibleInRanking = user.IsVisibleInRanking,
                Roles = roles,
                PermissionClaims = permissionClaims
            }
        };
    }

    public async Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        return await _roleRepository.UserHasPermissionAsync(userId, AuthConstants.Permissions.AdminAccess, cancellationToken);
    }

    public async Task<UserInfoResult> CheckTokenAsync(UserEntity currentUser, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var nextRank = _rankService.GetNextRank(currentUser.ProfileRank);
        var elo = await _eloRepository.GetLatestEloAsync(currentUser.Id, cancellationToken) ?? 1000;
        var roles = await _roleRepository.GetRoleNamesByUserIdAsync(currentUser.Id, cancellationToken);
        var permissionClaims = await _roleRepository.GetPermissionClaimsByUserIdAsync(currentUser.Id, cancellationToken);

        return new UserInfoResult
        {
            Name = currentUser.Name,
            Id = currentUser.Id,
            Email = currentUser.Email,
            Avatar = currentUser.Avatar,
            ProfileRank = currentUser.ProfileRank,
            CreatedAt = currentUser.CreatedAt.UtcDateTime,
            UpdatedAt = currentUser.UpdatedAt.UtcDateTime,
            Elo = elo,
            NextRank = nextRank == null ? null : new RankInfo { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
            IsDeleted = currentUser.IsDeleted,
            IsVisibleInRanking = currentUser.IsVisibleInRanking,
            Roles = roles,
            PermissionClaims = permissionClaims
        };
    }

    public async Task<List<RankingEntry>> GetUsersRankingAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetRankingAsync(cancellationToken);
        if (users.Count == 0)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return users.Select(u => new RankingEntry
        {
            Name = u.User.Name,
            Avatar = u.User.Avatar,
            Elo = u.Elo,
            ProfileRank = u.User.ProfileRank
        }).ToList();
    }

    public async Task<int> GetUserEloAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var result = await _eloRepository.GetLatestEloAsync(userId, cancellationToken);
        if (!result.HasValue)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        return result.Value;
    }

    public async Task DeleteAccountAsync(UserEntity currentUser, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        currentUser.Email = $"anonymized_{currentUser.Id}@example.com";
        currentUser.Name = $"anonymized_user_{currentUser.Id}";
        currentUser.IsDeleted = true;

        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task LogoutAsync(UserEntity currentUser, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        _userSessionCache.Remove(currentUser.Id);
        return Task.CompletedTask;
    }

    public async Task ChangeVisibilityInRankingAsync(UserEntity currentUser, bool isVisibleInRanking, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            throw AppException.BadRequest(Messages.DidntFind);
        }

        currentUser.IsVisibleInRanking = isVisibleInRanking;
        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserRolesAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var normalizedRoleNames = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rolesToSet = await _roleRepository.GetByNamesAsync(normalizedRoleNames, cancellationToken);
        if (rolesToSet.Count != normalizedRoleNames.Count)
        {
            throw AppException.BadRequest(Messages.InvalidRoleSelection);
        }

        await _roleRepository.ReplaceUserRolesAsync(userId, rolesToSet.Select(r => r.Id).ToList(), cancellationToken);
    }
}
