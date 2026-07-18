using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;
using UserSessionEntity = LgymApi.Domain.Entities.UserSession;

namespace LgymApi.Application.Features.User;

public sealed partial class UserService : IUserService
{
    public async Task<Result<UserInfoResult, AppError>> CheckTokenAsync(UserEntity? currentUser, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<UserInfoResult, AppError>.Failure(new UserNotFoundError(Messages.DidntFind));
        }

        var nextRank = _rankService.GetNextRank(currentUser.ProfileRank);
        var roles = await _roleRepository.GetRoleNamesByUserIdAsync(currentUser.Id, cancellationToken);
        var permissionClaims = await _roleRepository.GetPermissionClaimsByUserIdAsync(currentUser.Id, cancellationToken);
        var hasActiveTutorials = await _tutorialService.HasActiveTutorialsAsync(currentUser.Id, cancellationToken);

        return Result<UserInfoResult, AppError>.Success(new UserInfoResult
        {
            Name = currentUser.Name,
            Id = currentUser.Id,
            Email = currentUser.Email,
            Avatar = currentUser.Avatar,
            ProfileRank = currentUser.ProfileRank,
            PreferredTimeZone = string.IsNullOrWhiteSpace(currentUser.PreferredTimeZone) ? _appDefaultsOptions.PreferredTimeZone : currentUser.PreferredTimeZone,
            CreatedAt = currentUser.CreatedAt.UtcDateTime,
            UpdatedAt = currentUser.UpdatedAt.UtcDateTime,
            Elo = 1000,
            NextRank = nextRank == null ? null : new RankInfo { Name = nextRank.Name, NeedElo = nextRank.NeedElo },
            IsDeleted = currentUser.IsDeleted,
            IsVisibleInRanking = currentUser.IsVisibleInRanking,
            Roles = roles,
                PermissionClaims = permissionClaims,
                HasActiveTutorials = hasActiveTutorials
        });
    }


    public async Task<Result<Unit, AppError>> DeleteAccountAsync(UserEntity? currentUser, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new UserNotFoundError(Messages.DidntFind));
        }

        currentUser.Email = $"anonymized_{currentUser.Id}@example.com";
        currentUser.Name = $"anonymized_user_{currentUser.Id}";
        currentUser.IsDeleted = true;

        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> LogoutAsync(UserEntity? currentUser, Id<UserSessionEntity>? sessionId, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new UserNotFoundError(Messages.DidntFind));
        }

        if (sessionId.HasValue)
        {
            await _userSessionStore.RevokeSessionAsync(sessionId.Value, cancellationToken);
            await DisassociateInstallationsForSessionAsync(sessionId.Value, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> ChangeVisibilityInRankingAsync(UserEntity? currentUser, bool isVisibleInRanking, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.DidntFind));
        }

        currentUser.IsVisibleInRanking = isVisibleInRanking;
        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }

    public async Task<Result<Unit, AppError>> UpdateTimeZoneAsync(UserEntity? currentUser, string preferredTimeZone, CancellationToken cancellationToken = default)
    {
        if (currentUser == null)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.DidntFind));
        }

        if (string.IsNullOrWhiteSpace(preferredTimeZone))
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.FieldRequired));
        }

        var normalizedPreferredTimeZone = preferredTimeZone.Trim();
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalizedPreferredTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.InvalidTimeZone));
        }
        catch (InvalidTimeZoneException)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.InvalidTimeZone));
        }

        currentUser.PreferredTimeZone = normalizedPreferredTimeZone;
        await _userRepository.UpdateAsync(currentUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
