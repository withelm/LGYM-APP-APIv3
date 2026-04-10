using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.User;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserServiceTests
{
    [Test]
    public async Task GetUserEloAsync_ReturnsInvalidUserError_WhenUserIdIsEmpty()
    {
        var mockDeps = Substitute.For<IUserServiceDependencies>();
        var service = new UserService(mockDeps);

        var result = await service.GetUserEloAsync(Id<User>.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<InvalidUserError>());
        });
    }
}
