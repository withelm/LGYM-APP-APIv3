using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.User;

public sealed partial class UserService : IUserService
{
    public async Task<bool> IsAdminAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsEmpty)
        {
            return false;
        }

        return await _roleRepository.UserHasPermissionAsync(userId, AuthConstants.Permissions.AdminAccess, cancellationToken);
    }

    public async Task<Result<List<RankingEntry>, AppError>> GetUsersRankingAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetRankingAsync(cancellationToken);
        if (users.Count == 0)
        {
            return Result<List<RankingEntry>, AppError>.Failure(new UserNotFoundError(Messages.DidntFind));
        }

        return Result<List<RankingEntry>, AppError>.Success(users.Select(u => new RankingEntry
        {
            Name = u.User.Name,
            Avatar = u.User.Avatar,
            Elo = u.Elo,
            ProfileRank = u.User.ProfileRank
        }).ToList());
    }

    public async Task<Result<Unit, AppError>> UpdateUserRolesAsync(Id<UserEntity> targetUserId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        if (targetUserId.IsEmpty)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.FieldRequired));
        }

        var user = await _userRepository.FindByIdAsync((Id<LgymApi.Domain.Entities.User>)targetUserId, cancellationToken);
        if (user == null)
        {
            return Result<Unit, AppError>.Failure(new UserNotFoundError(Messages.DidntFind));
        }

        var normalizedRoleNames = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rolesToSet = await _roleRepository.GetByNamesAsync(normalizedRoleNames, cancellationToken);
        if (rolesToSet.Count != normalizedRoleNames.Count)
        {
            return Result<Unit, AppError>.Failure(new InvalidUserError(Messages.InvalidRoleSelection));
        }

        await _roleRepository.ReplaceUserRolesAsync(targetUserId, rolesToSet.Select(r => r.Id).ToList(), cancellationToken);
        return Result<Unit, AppError>.Success(Unit.Value);
    }
}
