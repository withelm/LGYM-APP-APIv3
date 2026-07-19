using FluentAssertions;
using LgymApi.Application.Features.User;
using LgymApi.Application.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserServiceTests
{
    private IUserServiceDependencies _dependencies = null!;
    private IPushInstallationSessionDisassociationService _pushInstallationSessionDisassociationService = null!;
    private UserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _dependencies = Substitute.For<IUserServiceDependencies>();
        _pushInstallationSessionDisassociationService = Substitute.For<IPushInstallationSessionDisassociationService>();
        _dependencies.PushInstallationSessionDisassociationService.Returns(_pushInstallationSessionDisassociationService);
        _service = new UserService(_dependencies);
    }

    [Test]
    public async Task LogoutAsync_RevokesSessionStagesDisassociationAndCommitsExactlyOnce()
    {
        var sessionId = Id<UserSession>.New();
        var currentUser = new User { Id = Id<User>.New(), Name = "user", Email = "user@example.com" };

        var result = await _service.LogoutAsync(currentUser, sessionId);

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _dependencies.UserSessionStore.RevokeSessionAsync(sessionId, Arg.Any<CancellationToken>());
            _pushInstallationSessionDisassociationService.StageDisassociateForSessionAsync(sessionId, Arg.Any<CancellationToken>());
            _dependencies.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await _dependencies.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
