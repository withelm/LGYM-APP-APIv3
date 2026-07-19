using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Identity.Profile;
using LgymApi.Application.Identity.Ranking;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Services;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserServiceProfileTests
{
    private IRoleRepository _roleRepository = null!;
    private ITutorialService _tutorialService = null!;
    private IUnitOfWork _unitOfWork = null!;
    private IUserRepository _userRepository = null!;
    private IMapper _mapper = null!;
    private IRankService _rankService = null!;
    private UserProfileService _profileService = null!;
    private UserRankingService _rankingService = null!;

    [SetUp]
    public void SetUp()
    {
        _roleRepository = Substitute.For<IRoleRepository>();
        _tutorialService = Substitute.For<ITutorialService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _userRepository = Substitute.For<IUserRepository>();
        _mapper = BuildMapper();
        _rankService = Substitute.For<IRankService>();
        _userRepository.UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));
        _profileService = new UserProfileService(new UserProfileServiceDependencies(
            _userRepository,
            _roleRepository,
            _rankService,
            _unitOfWork,
            new AppDefaultsOptions { PreferredTimeZone = "UTC" },
            _tutorialService,
            _mapper));
        _rankingService = new UserRankingService(_userRepository, _unitOfWork, _mapper);
    }

    [Test]
    public async Task CheckTokenAsync_ProjectsRolesClaimsTutorialStateAndDefaultTimeZone()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var user = CreateUser();
        user.PreferredTimeZone = string.Empty;
        _roleRepository.GetRoleNamesByUserIdAsync(user.Id, cancellationToken).Returns(["User", "Trainer"]);
        _roleRepository.GetPermissionClaimsByUserIdAsync(user.Id, cancellationToken).Returns(["users.read"]);
        _tutorialService.HasActiveTutorialsAsync(user.Id, cancellationToken).Returns(true);
        _rankService.GetNextRank(user.ProfileRank).Returns(new RankDefinition { Name = "Senior 1", NeedElo = 1500 });

        var result = await _profileService.CheckTokenAsync(user, cancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Name.Should().Be(user.Name);
        result.Value.Email.Should().Be(user.Email);
        result.Value.Avatar.Should().Be(user.Avatar);
        result.Value.ProfileRank.Should().Be(user.ProfileRank);
        result.Value.Roles.Should().Equal("User", "Trainer");
        result.Value.PermissionClaims.Should().Equal("users.read");
        result.Value.HasActiveTutorials.Should().BeTrue();
        result.Value.PreferredTimeZone.Should().Be("UTC");
        result.Value.Elo.Should().Be(1000);
        result.Value.NextRank.Should().BeEquivalentTo(new RankInfo { Name = "Senior 1", NeedElo = 1500 });
        result.Value.CreatedAt.Should().Be(user.CreatedAt.UtcDateTime);
        result.Value.UpdatedAt.Should().Be(user.UpdatedAt.UtcDateTime);
        result.Value.IsDeleted.Should().Be(user.IsDeleted);
        result.Value.IsVisibleInRanking.Should().Be(user.IsVisibleInRanking);
        await _roleRepository.Received(1).GetRoleNamesByUserIdAsync(user.Id, cancellationToken);
    }

    [Test]
    public async Task DeleteAccountAsync_AnonymizesUserAndCommits()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var user = CreateUser();

        var result = await _profileService.DeleteAccountAsync(user, cancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.IsDeleted.Should().BeTrue();
        user.Name.Should().Be($"anonymized_user_{user.Id}");
        user.Email.Value.Should().Be($"anonymized_{user.Id}@example.com");
        await _userRepository.Received(1).UpdateAsync(user, cancellationToken);
        await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
    }

    [Test]
    public async Task ChangeVisibilityInRankingAsync_UpdatesVisibilityAndCommits()
    {
        var user = CreateUser();

        var result = await _rankingService.ChangeVisibilityInRankingAsync(user, false);

        result.IsSuccess.Should().BeTrue();
        user.IsVisibleInRanking.Should().BeFalse();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateTimeZoneAsync_ReturnsInvalidUserErrorWithoutUpdateOrCommit_WhenTimeZoneIsInvalid()
    {
        var user = CreateUser();

        var result = await _profileService.UpdateTimeZoneAsync(user, "Not/ARealTimeZone");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidUserError>();
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetUsersRankingAsync_MapsRepositoryRankingEntries()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var user = CreateUser();
        user.Avatar = "avatar";
        user.ProfileRank = "Senior 1";
        _userRepository.GetRankingAsync(cancellationToken).Returns([new UserRankingEntry(user, 1450)]);

        var result = await _rankingService.GetUsersRankingAsync(cancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Name.Should().Be(user.Name);
        result.Value[0].Avatar.Should().Be("avatar");
        result.Value[0].Elo.Should().Be(1450);
        result.Value[0].ProfileRank.Should().Be("Senior 1");
        await _userRepository.Received(1).GetRankingAsync(cancellationToken);
    }

    private static User CreateUser() => new()
    {
        Id = Id<User>.New(),
        Name = "user",
        Email = "user@example.com",
        ProfileRank = "Junior 1",
        PreferredTimeZone = "Europe/Warsaw"
    };

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(IMappingProfile).Assembly);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMapper>();
    }
}
