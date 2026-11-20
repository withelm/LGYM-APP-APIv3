using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Application.Features.User.Models;
using LgymApi.Application.Identity.Contracts.BackgroundCommands;
using LgymApi.Application.Identity.Contracts.Authentication;
using LgymApi.Application.Identity.Contracts.Registration;
using LgymApi.Application.Identity.Authentication;
using LgymApi.Application.Identity.Registration;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Notifications;
using LgymApi.Application.Options;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.Services;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using LgymApi.UnitTests.Fakes;
using LgymApi.TestUtils.Fakes;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserServiceAuthTests
{
    private ICommandDispatcher _commandDispatcher = null!;
    private ILegacyPasswordService _passwordService = null!;
    private ConfigurableRoleRepository _roleRepository = null!;
    private ConfigurableTokenService _tokenService = null!;
    private ITutorialService _tutorialService = null!;
    private IUnitOfWork _unitOfWork = null!;
    private ConfigurableUserRepository _userRepository = null!;
    private FakeUserSessionStore _userSessionStore = null!;
    private IRankService _rankService = null!;
    private IUserCredentialLoginService _credentialLoginService = null!;
    private IUserRegistrationService _registrationService = null!;

    [SetUp]
    public void SetUp()
    {
        _commandDispatcher = Substitute.For<ICommandDispatcher>();
        _passwordService = Substitute.For<ILegacyPasswordService>();
        _roleRepository = new ConfigurableRoleRepository();
        _tokenService = new ConfigurableTokenService();
        _tutorialService = Substitute.For<ITutorialService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _userRepository = new ConfigurableUserRepository();
        _userSessionStore = new FakeUserSessionStore();
        _rankService = Substitute.For<IRankService>();
        _passwordService.Create(Arg.Any<string>()).Returns(("hash", "salt", 25000, 512, "sha256"));
        _registrationService = new UserRegistrationService(
            _userRepository,
            _roleRepository,
            _passwordService,
            _commandDispatcher,
            _unitOfWork,
            NullLogger<UserRegistrationService>.Instance,
            new AppDefaultsOptions { PreferredLanguage = "en-US", PreferredTimeZone = "UTC" },
            _tutorialService);
        _credentialLoginService = new UserCredentialLoginService(
            _userRepository,
            _roleRepository,
            _passwordService,
            _rankService,
            _userSessionStore,
            _tokenService,
            _unitOfWork,
            new AppDefaultsOptions { PreferredLanguage = "en-US", PreferredTimeZone = "UTC" },
            _tutorialService,
            BuildMapper(),
            new TrainerApplicationRepositoryStub());
    }

    [Test]
    public async Task RegisterAsync_AssignsDefaultRoleEnqueuesCommandAndCommits_WhenInputIsValid()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var defaultRole = new Role { Id = Id<Role>.New(), Name = AuthConstants.Roles.User };
        User? addedUser = null;
        _userRepository.FindByNameOrEmail = (_, _, _) => Task.FromResult<User?>(null);
        _userRepository.Add = (user, _) =>
        {
            addedUser = user;
            return Task.CompletedTask;
        };
        _roleRepository.GetByNames = (_, _) => Task.FromResult<List<Role>>([defaultRole]);
        _roleRepository.AddUserRoles = (_, _, _) => Task.CompletedTask;
        _commandDispatcher.EnqueueAsync(Arg.Any<UserRegisteredCommand>()).Returns(Task.CompletedTask);
        _tutorialService.InitializeOnboardingTutorialAsync(Arg.Any<Id<User>>(), cancellationToken).Returns(Result<Unit, AppError>.Success(Unit.Value));
        _unitOfWork.SaveChangesAsync(cancellationToken).Returns(Task.FromResult(1));

        var result = await _registrationService.RegisterAsync(new RegisterUserInput("new-user", " NEW@example.com ", "password123", "password123", null, null), cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(addedUser, Is.Not.Null);
        Assert.That(addedUser!.Email.Value, Is.EqualTo("new@example.com"));
        Assert.That(addedUser.PreferredLanguage, Is.EqualTo("en-US"));
        Assert.That(addedUser.PreferredTimeZone, Is.EqualTo("UTC"));
        Assert.That(addedUser.IsVisibleInRanking, Is.True);
        Assert.That(_roleRepository.Calls
            .Where(call =>
                call.Method == nameof(IRoleRepository.AddUserRolesAsync)
                && call.Argument is ValueTuple<Id<User>, IReadOnlyCollection<Id<Role>>> arguments
                && arguments.Item1 == addedUser.Id
                && arguments.Item2.Single() == defaultRole.Id
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
        await _commandDispatcher.Received(1).EnqueueAsync(Arg.Is<UserRegisteredCommand>(command => command.UserId == addedUser.Id));
        await _tutorialService.Received(1).InitializeOnboardingTutorialAsync(addedUser.Id, cancellationToken);
        await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
        Received.InOrder(() =>
        {
            _unitOfWork.SaveChangesAsync(cancellationToken);
            _tutorialService.InitializeOnboardingTutorialAsync(addedUser.Id, cancellationToken);
        });
    }

    [Test]
    public async Task RegisterAsync_WhenTutorialInitializationThrows_PropagatesFailure()
    {
        var defaultRole = new Role { Id = Id<Role>.New(), Name = AuthConstants.Roles.User };
        User? addedUser = null;
        _userRepository.FindByNameOrEmail = (_, _, _) => Task.FromResult<User?>(null);
        _userRepository.Add = (user, _) =>
        {
            addedUser = user;
            return Task.CompletedTask;
        };
        _roleRepository.GetByNames = (_, _) => Task.FromResult<List<Role>>([defaultRole]);
        _roleRepository.AddUserRoles = (_, _, _) => Task.CompletedTask;
        _commandDispatcher.EnqueueAsync(Arg.Any<UserRegisteredCommand>()).Returns(Task.CompletedTask);
        _tutorialService.InitializeOnboardingTutorialAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<Unit, AppError>>(new InvalidOperationException("tutorial failed")));

        var action = () => _registrationService.RegisterAsync(
            new RegisterUserInput("new-user", "new@example.com", "password123", "password123", true, null));

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("tutorial failed").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _tutorialService.Received(1).InitializeOnboardingTutorialAsync(addedUser!.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RegisterAsync_WhenTutorialInitializationFails_ReturnsFailure()
    {
        var defaultRole = new Role { Id = Id<Role>.New(), Name = AuthConstants.Roles.User };
        var tutorialError = new InternalServerError("tutorial failed");
        _userRepository.FindByNameOrEmail = (_, _, _) => Task.FromResult<User?>(null);
        _userRepository.Add = (_, _) => Task.CompletedTask;
        _roleRepository.GetByNames = (_, _) => Task.FromResult<List<Role>>([defaultRole]);
        _roleRepository.AddUserRoles = (_, _, _) => Task.CompletedTask;
        _commandDispatcher.EnqueueAsync(Arg.Any<UserRegisteredCommand>()).Returns(Task.CompletedTask);
        _tutorialService.InitializeOnboardingTutorialAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, AppError>.Failure(tutorialError));

        var result = await _registrationService.RegisterAsync(
            new RegisterUserInput("new-user", "new@example.com", "password123", "password123", true, null));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.SameAs(tutorialError));
    }

    [Test]
    public async Task RegisterTrainerAsync_CreatesPendingApplicationWithOnlyUserRoleAndHidesRanking()
    {
        var roles = new List<Role>
        {
            new() { Id = Id<Role>.New(), Name = AuthConstants.Roles.User }
        };
        User? addedUser = null;
        _userRepository.FindByNameOrEmail = (_, _, _) => Task.FromResult<User?>(null);
        _userRepository.Add = (user, _) =>
        {
            addedUser = user;
            return Task.CompletedTask;
        };
        _roleRepository.GetByNames = (_, _) => Task.FromResult(roles);
        _roleRepository.AddUserRoles = (_, _, _) => Task.CompletedTask;
        _commandDispatcher.EnqueueAsync(Arg.Any<UserRegisteredCommand>()).Returns(Task.CompletedTask);
        _tutorialService.InitializeOnboardingTutorialAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>()).Returns(Result<Unit, AppError>.Success(Unit.Value));

        var result = await _registrationService.RegisterTrainerAsync(new RegisterUserInput("trainer", "trainer@example.com", "password123", "password123", true, "pl-PL"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(addedUser, Is.Not.Null);
        Assert.That(addedUser!.IsVisibleInRanking, Is.False);
        var assertedValue179 = (addedUser.TrainerApplications).ToArray();
        Assert.That(assertedValue179, Has.Exactly(1).Items);
        Assert.That(assertedValue179.Single().Status, Is.EqualTo(LgymApi.Domain.Enums.TrainerApprovalStatus.PendingApproval));
        Assert.That(addedUser.PreferredLanguage, Is.EqualTo("en-US"));
        Assert.That(_roleRepository.Calls
            .Where(call =>
                call.Method == nameof(IRoleRepository.AddUserRolesAsync)
                && call.Argument is ValueTuple<Id<User>, IReadOnlyCollection<Id<Role>>> arguments
                && arguments.Item1 == addedUser.Id
                && arguments.Item2.SequenceEqual(roles.Select(role => role.Id))), Has.Exactly(1).Items);
    }

    [Test]
    public async Task RegisterAsync_ReturnsInternalServerErrorWithoutCommit_WhenDefaultRoleIsMissing()
    {
        _userRepository.FindByNameOrEmail = (_, _, _) => Task.FromResult<User?>(null);
        _userRepository.Add = (_, _) => Task.CompletedTask;
        _roleRepository.GetByNames = (_, _) => Task.FromResult(new List<Role>());

        var result = await _registrationService.RegisterAsync(new RegisterUserInput("new-user", "new@example.com", "password123", "password123", true, null));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<InternalServerError>());
        await _commandDispatcher.DidNotReceive().EnqueueAsync(Arg.Any<UserRegisteredCommand>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LoginAsync_CreatesSessionProjectsClaimsAndCommits_WhenCredentialsAreValid()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var user = CreateUser("login-user", "login@example.com");
        user.PreferredTimeZone = string.Empty;
        var session = new UserSession { Id = Id<UserSession>.New(), UserId = user.Id, Jti = "session-jti" };
        var roles = new List<string> { AuthConstants.Roles.User };
        var permissionClaims = new List<string> { AuthConstants.Permissions.AdminAccess };
        _userRepository.FindByName = (_, _) => Task.FromResult<User?>(user);
        _passwordService.Verify("password123", user.LegacyHash!, user.LegacySalt!, user.LegacyIterations, user.LegacyKeyLength, user.LegacyDigest).Returns(true);
        _roleRepository.GetRoleNamesByUserId = (_, _) => Task.FromResult(roles);
        _roleRepository.GetPermissionClaimsByUserId = (_, _) => Task.FromResult(permissionClaims);
        _userSessionStore.CreateSessionHandler = (_, _, _) => Task.FromResult(session);
        _tokenService.Create = (_, _, _, _, _) => "issued-token";
        _tutorialService.HasActiveTutorialsAsync(user.Id, cancellationToken).Returns(true);
        _unitOfWork.SaveChangesAsync(cancellationToken).Returns(Task.FromResult(1));
        _rankService.GetNextRank(user.ProfileRank).Returns(new RankDefinition { Name = "Senior 1", NeedElo = 1500 });

        var result = await _credentialLoginService.LoginAsync(user.Name, "password123", cancellationToken);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Token, Is.EqualTo("issued-token"));
        Assert.That(result.Value.PermissionClaims, Is.EqualTo(permissionClaims));
        Assert.That(result.Value.User.Roles, Is.EqualTo(roles));
        Assert.That(result.Value.User.PermissionClaims, Is.EqualTo(permissionClaims));
        Assert.That(result.Value.User.PreferredTimeZone, Is.EqualTo("UTC"));
        Assert.That(result.Value.User.Elo, Is.EqualTo(1000));
        Assert.That(result.Value.User.NextRank, Is.EqualTo(new RankInfo { Name = "Senior 1", NeedElo = 1500 }).UsingPropertiesComparer());
        Assert.That(result.Value.User.CreatedAt, Is.EqualTo(user.CreatedAt.UtcDateTime));
        Assert.That(result.Value.User.UpdatedAt, Is.EqualTo(user.UpdatedAt.UtcDateTime));
        Assert.That(result.Value.User.IsDeleted, Is.EqualTo(user.IsDeleted));
        Assert.That(result.Value.User.IsVisibleInRanking, Is.EqualTo(user.IsVisibleInRanking));
        Assert.That(result.Value.User.HasActiveTutorials, Is.True);
        Assert.That(_userSessionStore.Calls
            .Where(call =>
                call.Method == nameof(IUserSessionStore.CreateSessionAsync)
                && call.Argument is ValueTuple<Id<User>, DateTimeOffset> arguments
                && arguments.Item1 == user.Id
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
        await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
    }

    [Test]
    public async Task LoginTrainerAsync_ReturnsUnauthorizedWithoutSessionOrCommit_WhenTrainerRoleIsMissing()
    {
        var user = CreateUser("user", "user@example.com");
        _userRepository.FindByName = (_, _) => Task.FromResult<User?>(user);
        _passwordService.Verify(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>()).Returns(true);
        _roleRepository.GetRoleNamesByUserId = (_, _) => Task.FromResult<List<string>>([AuthConstants.Roles.User]);

        var result = await _credentialLoginService.LoginTrainerAsync(user.Name, "password123");

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<UserUnauthorizedError>());
        Assert.That((_userSessionStore.Calls).Any(call => call.Method == nameof(IUserSessionStore.CreateSessionAsync)), Is.False);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static User CreateUser(string name, string email) => new()
    {
        Id = Id<User>.New(),
        Name = name,
        Email = email,
        ProfileRank = "Junior 1",
        LegacyHash = "hash",
        LegacySalt = "salt",
        LegacyIterations = 25000,
        LegacyKeyLength = 512,
        LegacyDigest = "sha256"
    };

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMapper>();
    }
}
