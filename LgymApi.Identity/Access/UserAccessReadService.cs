using LgymApi.Application.Identity.Contracts.Access;
using LgymApi.Identity.Approval;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Identity.Access;

internal sealed class UserAccessReadService : IUserAccessReadService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ITrainerApplicationRepository _trainerApplications;

    public UserAccessReadService(IUserRepository userRepository, IRoleRepository roleRepository,
        ITrainerApplicationRepository trainerApplications)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _trainerApplications = trainerApplications;
    }

    public async Task<bool> UserExistsAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default)
        => !userId.IsEmpty && await _userRepository.FindByIdAsync(userId, cancellationToken) != null;

    public async Task<bool> IsTrainerAsync(Id<LgymApi.Domain.Entities.User> userId, CancellationToken cancellationToken = default)
    {
        var user = userId.IsEmpty ? null : await _userRepository.FindByIdAsync(userId, cancellationToken);
        return user is not null
            && TrainerApprovalPolicy.IsApproved(user, await _trainerApplications.GetLatestStatusAsync(user.Id, cancellationToken))
            && await _roleRepository.UserHasRoleAsync(userId, AuthConstants.Roles.Trainer, cancellationToken);
    }
}
