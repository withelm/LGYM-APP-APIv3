using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Identity.Contracts.Administration;
using LgymApi.Application.Identity.Administration;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.UnitTests.Fakes;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserServiceAdminTests
{
    private ConfigurableRoleRepository _roleRepository = null!;
    private IUnitOfWork _unitOfWork = null!;
    private ConfigurableUserRepository _userRepository = null!;
    private UserAdminAccessService _adminAccessService = null!;
    private UserRoleAdministrationService _roleAdministrationService = null!;
    private TrainerApplicationRepositoryStub _trainerApplications = null!;

    [SetUp]
    public void SetUp()
    {
        _roleRepository = new ConfigurableRoleRepository();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _userRepository = new ConfigurableUserRepository();
        _adminAccessService = new UserAdminAccessService(_roleRepository);
        _trainerApplications = new TrainerApplicationRepositoryStub();
        _roleAdministrationService = new UserRoleAdministrationService(_userRepository, _roleRepository, _unitOfWork, _trainerApplications);
    }

    [Test]
    public async Task IsAdminAsync_ReturnsFalseWithoutRepositoryCall_WhenUserIdIsEmpty()
    {
        var result = await _adminAccessService.IsAdminAsync(Id<User>.Empty);

        Assert.That(result, Is.False);
        Assert.That((_roleRepository.Calls).Any(call => call.Method == nameof(IRoleRepository.UserHasPermissionAsync)), Is.False);
    }

    [Test]
    public async Task IsAdminAsync_ForwardsPermissionCheckAndCancellationToken()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var userId = Id<User>.New();
        _roleRepository.UserHasPermission = (_, _, _) => Task.FromResult(true);

        var result = await _adminAccessService.IsAdminAsync(userId, cancellationToken);

        Assert.That(result, Is.True);
        Assert.That(_roleRepository.Calls
            .Where(call =>
                call.Method == nameof(IRoleRepository.UserHasPermissionAsync)
                && call.Argument is ValueTuple<Id<User>, string> arguments
                && arguments.Item1 == userId
                && arguments.Item2 == AuthConstants.Permissions.AdminAccess
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
    }

    [Test]
    public async Task UpdateUserRolesAsync_ReplacesNormalizedRolesAndCommitsExactlyOnce()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var user = new User { Id = Id<User>.New(), Name = "user", Email = "user@example.com" };
        _trainerApplications.Status = TrainerApprovalStatus.Approved;
        var trainerRole = new Role { Id = Id<Role>.New(), Name = AuthConstants.Roles.Trainer };
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);
        _roleRepository.GetByNames = (_, _) => Task.FromResult<List<Role>>([trainerRole]);
        _roleRepository.ReplaceUserRoles = (_, _, _) => Task.CompletedTask;

        var result = await _roleAdministrationService.UpdateUserRolesAsync(user.Id, [" Trainer ", "trainer", ""], cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_roleRepository.Calls
            .Where(call =>
                call.Method == nameof(IRoleRepository.GetByNamesAsync)
                && call.Argument is IReadOnlyCollection<string> roles
                && roles.Count == 1
                && roles.Single() == AuthConstants.Roles.Trainer
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
        Assert.That(_roleRepository.Calls
            .Where(call =>
                call.Method == nameof(IRoleRepository.ReplaceUserRolesAsync)
                && call.Argument is ValueTuple<Id<User>, IReadOnlyCollection<Id<Role>>> arguments
                && arguments.Item1 == user.Id
                && arguments.Item2.Single() == trainerRole.Id
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
        await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
    }

    [Test]
    public async Task UpdateUserRolesAsync_ReturnsUserNotFoundWithoutCommit_WhenTargetUserIsMissing()
    {
        var userId = Id<User>.New();
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(null);

        var result = await _roleAdministrationService.UpdateUserRolesAsync(userId, [AuthConstants.Roles.User]);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<UserNotFoundError>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateUserRolesAsync_ReturnsInvalidUserErrorWithoutCommit_WhenRoleIsMissing()
    {
        var user = new User { Id = Id<User>.New(), Name = "user", Email = "user@example.com" };
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(user);
        _roleRepository.GetByNames = (_, _) => Task.FromResult<List<Role>>([]);

        var result = await _roleAdministrationService.UpdateUserRolesAsync(user.Id, ["missing"]);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidUserError>());
        Assert.That((_roleRepository.Calls).Any(call => call.Method == nameof(IRoleRepository.ReplaceUserRolesAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateUserRolesAsync_ReturnsInvalidUserErrorWithoutRepositoryCallsOrCommit_WhenTargetUserIdIsEmpty()
    {
        var result = await _roleAdministrationService.UpdateUserRolesAsync(Id<User>.Empty, [AuthConstants.Roles.User]);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidUserError>());
        Assert.That((_userRepository.Calls).Any(call => call.Method == nameof(IUserRepository.FindByIdAsync)), Is.False);
        Assert.That((_roleRepository.Calls).Any(call => call.Method == nameof(IRoleRepository.GetByNamesAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

}
