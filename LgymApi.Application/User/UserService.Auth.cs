using System.ComponentModel.DataAnnotations;
using System.Globalization;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;
using UserEntity = LgymApi.Domain.Entities.User;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.User;

public sealed partial class UserService : IUserService
{
    public async Task<Result<Unit, AppError>> RegisterAsync(RegisterUserInput input, CancellationToken cancellationToken = default)
    {
        return await RegisterCoreAsync(
            input,
            [AuthConstants.Roles.User],
            cancellationToken);
    }

    public async Task<Result<Unit, AppError>> RegisterTrainerAsync(RegisterUserInput input, CancellationToken cancellationToken = default)
    {
        var trainerInput = input with { IsVisibleInRanking = false, PreferredLanguage = null };

        return await RegisterCoreAsync(
            trainerInput,
            [AuthConstants.Roles.User, AuthConstants.Roles.Trainer],
            cancellationToken);
    }

    public async Task<Result<LoginResult, AppError>> LoginAsync(string name, string password, CancellationToken cancellationToken = default)
    {
        return await LoginCoreAsync(name, password, requiredRole: null, cancellationToken);
    }

    public async Task<Result<LoginResult, AppError>> LoginTrainerAsync(string name, string password, CancellationToken cancellationToken = default)
    {
        return await LoginCoreAsync(name, password, AuthConstants.Roles.Trainer, cancellationToken);
    }

    private async Task<Result<Unit, AppError>> RegisterCoreAsync(
        RegisterUserInput input,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken)
    {
        var name = input.Name;
        var email = input.Email;
        var password = input.Password;
        var confirmPassword = input.ConfirmPassword;

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.NameIsRequired));
        }

        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (!new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.EmailInvalid));
        }

        if (password.Length < 6)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.PasswordMin));
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.SamePassword));
        }

        var existingUser = await _userRepository.FindByNameOrEmailAsync(name, normalizedEmail!, cancellationToken);
        if (existingUser != null)
        {
            if (string.Equals(existingUser.Name, name, StringComparison.Ordinal))
            {
                return Result<Unit, AppError>.Failure(new ConflictError(Messages.UserWithThatName));
            }

            return Result<Unit, AppError>.Failure(new ConflictError(Messages.UserWithThatEmail));
        }

        var passwordData = _legacyPasswordService.Create(password);
        var user = new UserEntity
        {
            Id = Id<LgymApi.Domain.Entities.User>.New(),
            Name = name,
            Email = normalizedEmail!,
            IsVisibleInRanking = input.IsVisibleInRanking ?? true,
            ProfileRank = "Junior 1",
            LegacyHash = passwordData.Hash,
            LegacySalt = passwordData.Salt,
            LegacyIterations = passwordData.Iterations,
            LegacyKeyLength = passwordData.KeyLength,
            LegacyDigest = passwordData.Digest,
            PreferredLanguage = ResolvePreferredLanguage(input.PreferredLanguage),
            PreferredTimeZone = _appDefaultsOptions.PreferredTimeZone
        };

        await _userRepository.AddAsync(user, cancellationToken);

        var rolesToAssign = await _roleRepository.GetByNamesAsync(roleNames, cancellationToken);
        if (rolesToAssign.Count != roleNames.Count)
        {
            return Result<Unit, AppError>.Failure(new InternalServerError(Messages.DefaultRoleMissing));
        }

        await _roleRepository.AddUserRolesAsync(user.Id, rolesToAssign.Select(r => r.Id).ToList(), cancellationToken);

        await _eloRepository.AddAsync(new global::LgymApi.Domain.Entities.EloRegistry
        {
            Id = Id<LgymApi.Domain.Entities.EloRegistry>.New(),
            UserId = user.Id,
            Date = DateTimeOffset.UtcNow,
            Elo = 1000
        }, cancellationToken);

        await _commandDispatcher.EnqueueAsync(new UserRegisteredCommand { UserId = user.Id });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Initialize onboarding tutorial for new user
        try
        {
            await _tutorialService.InitializeOnboardingTutorialAsync(user.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to initialize onboarding tutorial for user {UserId}. Registration is still successful.",
                user.Id);
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }


    private async Task<Result<LoginResult, AppError>> LoginCoreAsync(string name, string password, string? requiredRole, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
        {
            return Result<LoginResult, AppError>.Failure(new UserUnauthorizedError(Messages.Unauthorized));
        }

        var user = await _userRepository.FindByNameAsync(name, cancellationToken);
        if (user == null || string.IsNullOrWhiteSpace(user.LegacyHash) || string.IsNullOrWhiteSpace(user.LegacySalt))
        {
            return Result<LoginResult, AppError>.Failure(new UserUnauthorizedError(Messages.Unauthorized));
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
            return Result<LoginResult, AppError>.Failure(new UserUnauthorizedError(Messages.Unauthorized));
        }

        var roles = await _roleRepository.GetRoleNamesByUserIdAsync(user.Id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(requiredRole) &&
            !roles.Contains(requiredRole, StringComparer.Ordinal))
        {
            return Result<LoginResult, AppError>.Failure(new UserUnauthorizedError(Messages.Unauthorized));
        }

        var permissionClaims = await _roleRepository.GetPermissionClaimsByUserIdAsync(user.Id, cancellationToken);
        var session = await _userSessionStore.CreateSessionAsync(user.Id, DateTimeOffset.UtcNow.AddDays(30), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
                PreferredTimeZone = string.IsNullOrWhiteSpace(user.PreferredTimeZone) ? _appDefaultsOptions.PreferredTimeZone : user.PreferredTimeZone,
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

    private string ResolvePreferredLanguage(string? preferredLanguageHeader)
    {
        if (string.IsNullOrWhiteSpace(preferredLanguageHeader))
        {
            return _appDefaultsOptions.PreferredLanguage;
        }

        var candidate = preferredLanguageHeader
            .Split(',')
            .Select(part => part.Split(';').FirstOrDefault()?.Trim())
            .FirstOrDefault(part => !string.IsNullOrWhiteSpace(part));

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return _appDefaultsOptions.PreferredLanguage;
        }

        try
        {
            return CultureInfo.GetCultureInfo(candidate).Name;
        }
        catch (CultureNotFoundException)
        {
            return _appDefaultsOptions.PreferredLanguage;
        }
    }
}
