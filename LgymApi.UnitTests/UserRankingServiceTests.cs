using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Errors;
using LgymApi.Application.Identity.Ranking;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.UnitTests.Fakes;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserRankingServiceTests
{
    [Test]
    public async Task ChangeVisibilityInRankingAsync_UpdatesIdentityAccountAndCommits()
    {
        var repository = new ConfigurableUserRepository();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.Update = (_, _) => Task.CompletedTask;
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        var user = new User { IsVisibleInRanking = true };
        var service = new UserRankingService(repository, unitOfWork);

        var result = await service.ChangeVisibilityInRankingAsync(user, false);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(user.IsVisibleInRanking, Is.False);
        Assert.That((repository.Calls).Where(call =>
            call.Method == nameof(IUserRepository.UpdateAsync)
            && call.Argument == user), Has.Exactly(1).Items);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ChangeVisibilityInRankingAsync_ReturnsInvalidUserErrorWithoutCommit_WhenCurrentUserIsMissing()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = new UserRankingService(new ConfigurableUserRepository(), unitOfWork);

        var result = await service.ChangeVisibilityInRankingAsync(null, true);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidUserError>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
