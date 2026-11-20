using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Errors;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Identity.Profile;
using LgymApi.Application.Identity.Ranking;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Services;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using LgymApi.UnitTests.Fakes;
using NSubstitute;
using LgymApi.Identity.Contracts.AccountDeletion;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserServiceProfileTests
{
    private ConfigurableRoleRepository _roleRepository = null!;
    private ITutorialService _tutorialService = null!;
    private IUnitOfWork _unitOfWork = null!;
    private ConfigurableUserRepository _userRepository = null!;
    private IRankService _rankService = null!;
    private IAccountDeletionService _accountDeletionService = null!;
    private UserProfileService _profileService = null!;
    private TrainerApplicationRepositoryStub _trainerApplications = null!;

    [SetUp]
    public void SetUp()
    {
        _roleRepository = new ConfigurableRoleRepository();
        _tutorialService = Substitute.For<ITutorialService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _userRepository = new ConfigurableUserRepository();
        _rankService = Substitute.For<IRankService>();
        _accountDeletionService = Substitute.For<IAccountDeletionService>();
        _trainerApplications = new TrainerApplicationRepositoryStub();
        _userRepository.Update = (_, _) => Task.CompletedTask;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));
        _profileService = new UserProfileService(
            _userRepository,
            _roleRepository,
            _rankService,
            _unitOfWork,
            _accountDeletionService,
            new AppDefaultsOptions { PreferredTimeZone = "UTC" },
            _tutorialService,
            BuildMapper(),
            _trainerApplications);
    }

    [Test]
    public async Task CheckTokenAsync_ProjectsRolesClaimsTutorialStateAndDefaultTimeZone()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var user = CreateUser();
        user.PreferredTimeZone = string.Empty;
        _trainerApplications.Status = TrainerApprovalStatus.Approved;
        _roleRepository.GetRoleNamesByUserId = (_, _) => Task.FromResult<List<string>>(["User", "Trainer"]);
        _roleRepository.GetPermissionClaimsByUserId = (_, _) => Task.FromResult<List<string>>(["users.read"]);
        _tutorialService.HasActiveTutorialsAsync(user.Id, cancellationToken).Returns(true);
        _rankService.GetNextRank(user.ProfileRank).Returns(new RankDefinition { Name = "Senior 1", NeedElo = 1500 });

        var result = await _profileService.CheckTokenAsync(user, cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Id, Is.EqualTo(user.Id));
        Assert.That(result.Value.Name, Is.EqualTo(user.Name));
        Assert.That(result.Value.Email, Is.EqualTo((string)user.Email));
        Assert.That(result.Value.Avatar, Is.EqualTo(user.Avatar));
        Assert.That(result.Value.ProfileRank, Is.EqualTo(user.ProfileRank));
        Assert.That(result.Value.Roles, Is.EqualTo(new[] { "User", "Trainer" }));
        Assert.That(result.Value.TrainerApprovalStatus, Is.EqualTo(TrainerApprovalStatus.Approved));
        Assert.That(result.Value.PermissionClaims, Is.EqualTo(new[] { "users.read" }));
        Assert.That(result.Value.HasActiveTutorials, Is.True);
        Assert.That(result.Value.PreferredTimeZone, Is.EqualTo("UTC"));
        Assert.That(result.Value.Elo, Is.EqualTo(1000));
        Assert.That(result.Value.NextRank, Is.EqualTo(new RankInfo { Name = "Senior 1", NeedElo = 1500 }).UsingPropertiesComparer());
        Assert.That(result.Value.CreatedAt, Is.EqualTo(user.CreatedAt.UtcDateTime));
        Assert.That(result.Value.UpdatedAt, Is.EqualTo(user.UpdatedAt.UtcDateTime));
        Assert.That(result.Value.IsDeleted, Is.EqualTo(user.IsDeleted));
        Assert.That(result.Value.IsVisibleInRanking, Is.EqualTo(user.IsVisibleInRanking));
        Assert.That(_roleRepository.Calls
            .Where(call =>
                call.Method == nameof(IRoleRepository.GetRoleNamesByUserIdAsync)
                && call.Argument is Id<User> id
                && id == user.Id
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
    }

    [Test]
    public async Task DeleteAccountAsync_UsesCanonicalLifecycleWithoutAnonymizingProfile()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var user = CreateUser();

        var result = await _profileService.DeleteAccountAsync(user, cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(user.IsDeleted, Is.False);
        Assert.That(user.Name, Is.EqualTo("user"));
        Assert.That(user.Email.Value, Is.EqualTo("user@example.com"));
        await _accountDeletionService.Received(1).RequestAsync(user.Id.Rebind<LgymApi.Identity.Contracts.AccountReference>(), cancellationToken);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(cancellationToken);
    }

    [Test]
    public async Task DeleteAccountAsync_PropagatesCancellationWithoutChangingProfile()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var user = CreateUser();
        cancellationSource.Cancel();
        _accountDeletionService.RequestAsync(
                user.Id.Rebind<LgymApi.Identity.Contracts.AccountReference>(),
                cancellationToken)
            .Returns(Task.FromCanceled(cancellationToken));

        var action = () => _profileService.DeleteAccountAsync(user, cancellationToken);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<OperationCanceledException>());
        Assert.That(user.Name, Is.EqualTo("user"));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateTimeZoneAsync_ReturnsInvalidUserErrorWithoutUpdateOrCommit_WhenTimeZoneIsInvalid()
    {
        var user = CreateUser();

        var result = await _profileService.UpdateTimeZoneAsync(user, "Not/ARealTimeZone");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InvalidUserError>());
        Assert.That((_userRepository.Calls).Any(call => call.Method == nameof(IUserRepository.UpdateAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
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
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMapper>();
    }
}
