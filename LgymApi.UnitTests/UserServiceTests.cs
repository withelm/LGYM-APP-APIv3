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

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.InstanceOf<InvalidUserError>());
        });
    }
}
