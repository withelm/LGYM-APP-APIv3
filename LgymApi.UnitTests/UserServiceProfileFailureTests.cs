using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Errors;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Identity.Profile;
using LgymApi.Application.Identity.Ranking;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.UnitTests.Fakes;
using NSubstitute;
using LgymApi.Identity.Contracts.AccountDeletion;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserServiceProfileFailureTests
{
    private IUnitOfWork _unitOfWork = null!;
    private ConfigurableUserRepository _userRepository = null!;
    private IAccountDeletionService _accountDeletionService = null!;
    private UserProfileService _profileService = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _userRepository = new ConfigurableUserRepository();
        _accountDeletionService = Substitute.For<IAccountDeletionService>();
        _profileService = new UserProfileService(
            _userRepository,
            new ConfigurableRoleRepository(),
            Substitute.For<LgymApi.Application.Services.IRankService>(),
            _unitOfWork,
            _accountDeletionService,
            new LgymApi.Application.Options.AppDefaultsOptions(),
            Substitute.For<LgymApi.Application.Features.Tutorial.ITutorialService>(),
            Substitute.For<IMapper>(),
            new TrainerApplicationRepositoryStub());
    }

    [Test]
    public async Task CheckTokenAsync_ReturnsUserNotFoundWithoutCommit_WhenCurrentUserIsMissing()
    {
        var result = await _profileService.CheckTokenAsync(null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<UserNotFoundError>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAccountAsync_ReturnsUserNotFoundWithoutCommit_WhenCurrentUserIsMissing()
    {
        var result = await _profileService.DeleteAccountAsync(null);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<UserNotFoundError>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAccountAsync_LeavesUserUnchangedWithoutCommit_WhenLifecycleRequestFails()
    {
        var user = new User { Id = Id<User>.New(), Name = "profile-failure", Email = "profile-failure@example.com" };
        var stagingFailure = new InvalidOperationException("Lifecycle request failed.");
        _accountDeletionService.RequestAsync(
                user.Id.Rebind<LgymApi.Identity.Contracts.AccountReference>(),
                CancellationToken.None)
            .Returns(Task.FromException(stagingFailure));

        var action = () => _profileService.DeleteAccountAsync(user);

        var exception = await Assert.CatchAsync<InvalidOperationException>(async () => { await action(); });
        Assert.That(exception, Is.SameAs(stagingFailure));
        Assert.That(user.IsDeleted, Is.False);
        Assert.That(user.Name, Is.EqualTo("profile-failure"));
        Assert.That(user.Email.Value, Is.EqualTo("profile-failure@example.com"));
        Assert.That((_userRepository.Calls).Any(call => call.Method == nameof(IUserRepository.UpdateAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

}
