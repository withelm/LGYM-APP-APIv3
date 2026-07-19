using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Identity.Profile;
using LgymApi.Application.Identity.Ranking;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserServiceProfileFailureTests
{
    private IUnitOfWork _unitOfWork = null!;
    private IUserRepository _userRepository = null!;
    private UserProfileService _profileService = null!;
    private UserRankingService _rankingService = null!;

    [SetUp]
    public void SetUp()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _userRepository = Substitute.For<IUserRepository>();
        _profileService = new UserProfileService(new UserProfileServiceDependencies(
            _userRepository,
            Substitute.For<IRoleRepository>(),
            Substitute.For<LgymApi.Application.Services.IRankService>(),
            _unitOfWork,
            new LgymApi.Application.Options.AppDefaultsOptions(),
            Substitute.For<LgymApi.Application.Features.Tutorial.ITutorialService>(),
            Substitute.For<IMapper>()));
        _rankingService = new UserRankingService(_userRepository, _unitOfWork, Substitute.For<IMapper>());
    }

    [Test]
    public async Task CheckTokenAsync_ReturnsUserNotFoundWithoutCommit_WhenCurrentUserIsMissing()
    {
        var result = await _profileService.CheckTokenAsync(null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<UserNotFoundError>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteAccountAsync_ReturnsUserNotFoundWithoutCommit_WhenCurrentUserIsMissing()
    {
        var result = await _profileService.DeleteAccountAsync(null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<UserNotFoundError>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ChangeVisibilityInRankingAsync_ReturnsInvalidUserErrorWithoutCommit_WhenCurrentUserIsMissing()
    {
        var result = await _rankingService.ChangeVisibilityInRankingAsync(null, true);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidUserError>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetUsersRankingAsync_ReturnsUserNotFoundWithoutCommit_WhenRankingIsEmpty()
    {
        _userRepository.GetRankingAsync(Arg.Any<CancellationToken>()).Returns(new List<LgymApi.Application.Models.UserRankingEntry>());

        var result = await _rankingService.GetUsersRankingAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<UserNotFoundError>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
