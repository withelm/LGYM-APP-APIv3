using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserServiceTests
{
    private IUserServiceDependencies _deps = null!;
    private UserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _deps = Substitute.For<IUserServiceDependencies>();
        _service = new UserService(_deps);
    }

    [Test]
    public async Task Should_ReturnInvalidUserError_When_UserIdIsEmpty()
    {
        var result = await _service.GetUserEloAsync(Id<User>.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidUserError>();
    }

    [Test]
    public async Task RegisterPushInstallation_WhenUnauthenticated_ReturnsUnauthorizedError()
    {
        var result = await _service.RegisterPushInstallationAsync(
            null,
            Id<UserSession>.New(),
            new RegisterPushInstallationInput("device-1", "android", "token-1", "1.0.0", "development", "authorized"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<UserUnauthorizedError>();
        result.Error.Message.Should().Be(Messages.Unauthorized);
    }

    [Test]
    public async Task RegisterPushInstallation_ReusesExistingInstallationAndClearsDisabledMetadata()
    {
        var userId = Id<User>.New();
        var sessionId = Id<UserSession>.New();
        var installation = new PushInstallation
        {
            Id = Id<PushInstallation>.New(),
            InstallationId = "device-1",
            Platform = "android",
            FcmToken = "old-token",
            Environment = "development",
            DisabledAt = DateTimeOffset.UtcNow.AddDays(-1),
            DisabledReason = "Unregistered"
        };
        var currentUser = new User { Id = userId, Name = "user", Email = "user@example.com" };

        _deps.PushInstallationRepository.FindByInstallationIdAsync("device-1", Arg.Any<CancellationToken>())
            .Returns(installation);

        var result = await _service.RegisterPushInstallationAsync(
            currentUser,
            sessionId,
            new RegisterPushInstallationInput("device-1", "ios", "new-token", "2.0.0", "production", "authorized"));

        result.IsFailure.Should().BeFalse();
        installation.UserId.Should().Be(userId);
        installation.SessionId.Should().Be(sessionId);
        installation.Platform.Should().Be("ios");
        installation.FcmToken.Should().Be("new-token");
        installation.AppVersion.Should().Be("2.0.0");
        installation.Environment.Should().Be("production");
        installation.PermissionStatus.Should().Be("authorized");
        installation.DisabledAt.Should().BeNull();
        installation.DisabledReason.Should().BeNull();
        await _deps.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LogoutAsync_RevokesSessionAndDisassociatesInstallationsBoundToSession()
    {
        var userId = Id<User>.New();
        var sessionId = Id<UserSession>.New();
        var currentUser = new User { Id = userId, Name = "user", Email = "user@example.com" };
        var installation = new PushInstallation
        {
            Id = Id<PushInstallation>.New(),
            UserId = userId,
            SessionId = sessionId,
            InstallationId = "device-1",
            Platform = "android",
            FcmToken = "token-1",
            Environment = "production"
        };

        _deps.PushInstallationRepository.GetBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns([installation]);

        var result = await _service.LogoutAsync(currentUser, sessionId);

        result.IsFailure.Should().BeFalse();
        installation.UserId.Should().BeNull();
        installation.SessionId.Should().BeNull();
        await _deps.UserSessionStore.Received(1).RevokeSessionAsync(sessionId, Arg.Any<CancellationToken>());
        await _deps.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
