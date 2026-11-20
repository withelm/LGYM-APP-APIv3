using LgymApi.Application.Identity.Adapters;
using LgymApi.Identity;
using LgymApi.Application.Platform.ReferenceData.AppConfig.Contracts;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Platform.Contracts;
using LgymApi.UnitTests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AppConfigAuthorizationAdapterTests
{
    private ConfigurableUserRepository _userRepository = null!;
    private ConfigurableRoleRepository _roleRepository = null!;
    private AppConfigAuthorizationAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new ConfigurableUserRepository();
        _roleRepository = new ConfigurableRoleRepository();
        _adapter = new AppConfigAuthorizationAdapter(_userRepository, _roleRepository);
    }

    [Test]
    public async Task CanManageAppConfigAsync_EmptyUserId_ReturnsFalseWithoutRepositoryCalls()
    {
        var result = await _adapter.CanManageAppConfigAsync(Id<ActorReference>.Empty);

        Assert.That(result, Is.False);
        Assert.That((_userRepository.Calls).Any(call => call.Method == nameof(IUserRepository.FindByIdAsync)), Is.False);
        Assert.That((_roleRepository.Calls).Any(call => call.Method == nameof(IRoleRepository.UserHasPermissionAsync)), Is.False);
    }

    [Test]
    public async Task CanManageAppConfigAsync_MissingUser_ReturnsFalseWithoutPermissionCheck()
    {
        var actorId = Id<ActorReference>.New();
        var userId = actorId.Rebind<User>();
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(null);

        var result = await _adapter.CanManageAppConfigAsync(actorId);

        Assert.That(result, Is.False);
        Assert.That(_userRepository.Calls
            .Where(call =>
                call.Method == nameof(IUserRepository.FindByIdAsync)
                && call.Argument is Id<User> id
                && id == userId
                && call.CancellationToken == CancellationToken.None), Has.Exactly(1).Items);
        Assert.That((_roleRepository.Calls).Any(call => call.Method == nameof(IRoleRepository.UserHasPermissionAsync)), Is.False);
    }

    [Test]
    public async Task CanManageAppConfigAsync_UserLacksManageAppConfigPermission_ReturnsFalse()
    {
        var actorId = Id<ActorReference>.New();
        var userId = actorId.Rebind<User>();
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(new User());
        _roleRepository.UserHasPermission = (_, _, _) => Task.FromResult(false);

        var result = await _adapter.CanManageAppConfigAsync(actorId);

        Assert.That(result, Is.False);
        Assert.That(_roleRepository.Calls
            .Where(call =>
                call.Method == nameof(IRoleRepository.UserHasPermissionAsync)
                && call.Argument is ValueTuple<Id<User>, string> arguments
                && arguments.Item1 == userId
                && arguments.Item2 == AuthConstants.Permissions.ManageAppConfig
                && call.CancellationToken == CancellationToken.None), Has.Exactly(1).Items);
    }

    [Test]
    public async Task CanManageAppConfigAsync_UserHasManageAppConfigPermission_ReturnsTrue()
    {
        var actorId = Id<ActorReference>.New();
        var userId = actorId.Rebind<User>();
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(new User());
        _roleRepository.UserHasPermission = (_, _, _) => Task.FromResult(true);

        var result = await _adapter.CanManageAppConfigAsync(actorId);

        Assert.That(result, Is.True);
        Assert.That(_roleRepository.Calls
            .Where(call =>
                call.Method == nameof(IRoleRepository.UserHasPermissionAsync)
                && call.Argument is ValueTuple<Id<User>, string> arguments
                && arguments.Item1 == userId
                && arguments.Item2 == AuthConstants.Permissions.ManageAppConfig
                && call.CancellationToken == CancellationToken.None), Has.Exactly(1).Items);
    }

    [Test]
    public async Task CanManageAppConfigAsync_AllowedUser_ForwardsCancellationTokenToBothRepositories()
    {
        var actorId = Id<ActorReference>.New();
        var userId = actorId.Rebind<User>();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        cancellationTokenSource.Cancel();
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(new User());
        _roleRepository.UserHasPermission = (_, _, _) => Task.FromResult(true);

        var result = await _adapter.CanManageAppConfigAsync(actorId, cancellationToken);

        Assert.That(result, Is.True);
        Assert.That(_userRepository.Calls
            .Where(call =>
                call.Method == nameof(IUserRepository.FindByIdAsync)
                && call.Argument is Id<User> id
                && id == userId
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
        Assert.That(_roleRepository.Calls
            .Where(call =>
                call.Method == nameof(IRoleRepository.UserHasPermissionAsync)
                && call.Argument is ValueTuple<Id<User>, string> arguments
                && arguments.Item1 == userId
                && arguments.Item2 == AuthConstants.Permissions.ManageAppConfig
                && call.CancellationToken == cancellationToken), Has.Exactly(1).Items);
    }

    [Test]
    public async Task CanManageAppConfigAsync_UserRepositoryThrows_PropagatesException()
    {
        var expectedException = new InvalidOperationException("User lookup failed.");
        _userRepository.FindById = (_, _) => Task.FromException<User?>(expectedException);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _adapter.CanManageAppConfigAsync(Id<ActorReference>.New()));

        Assert.That(exception, Is.SameAs(expectedException));
        Assert.That((_roleRepository.Calls).Any(call => call.Method == nameof(IRoleRepository.UserHasPermissionAsync)), Is.False);
    }

    [Test]
    public async Task CanManageAppConfigAsync_RoleRepositoryThrows_PropagatesException()
    {
        var actorId = Id<ActorReference>.New();
        var userId = actorId.Rebind<User>();
        var expectedException = new InvalidOperationException("Permission lookup failed.");
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(new User());
        _roleRepository.UserHasPermission = (_, _, _) => Task.FromException<bool>(expectedException);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _adapter.CanManageAppConfigAsync(actorId));

        Assert.That(exception, Is.SameAs(expectedException));
    }

    [Test]
    public async Task AddIdentityModule_RegistersAppConfigAuthorizationPortExactlyOnceAsScopedAndExecutesIt()
    {
        var services = new ServiceCollection();
        services.AddIdentityModule();
        services.AddScoped<IUserRepository>(_ => _userRepository);
        services.AddScoped<IRoleRepository>(_ => _roleRepository);

        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IAppConfigAuthorizationPort))
            .ToArray();

        Assert.That(registrations, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(registrations[0].Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
            Assert.That(registrations[0].ImplementationType, Is.EqualTo(typeof(AppConfigAuthorizationAdapter)));
        });

        using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        using var scope = serviceProvider.CreateScope();
        var port = scope.ServiceProvider.GetRequiredService<IAppConfigAuthorizationPort>();
        var actorId = Id<ActorReference>.New();
        var userId = actorId.Rebind<User>();
        _userRepository.FindById = (_, _) => Task.FromResult<User?>(new User());
        _roleRepository.UserHasPermission = (_, _, _) => Task.FromResult(true);

        var result = await port.CanManageAppConfigAsync(actorId);

        Assert.That(result, Is.True);

        _roleRepository.UserHasPermission = (_, _, _) => Task.FromResult(false);

        var denied = await port.CanManageAppConfigAsync(actorId);

        Assert.That(denied, Is.False);

        var expectedException = new InvalidOperationException("Permission lookup failed.");
        _roleRepository.UserHasPermission = (_, _, _) => Task.FromException<bool>(expectedException);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await port.CanManageAppConfigAsync(actorId));

        Assert.That(exception, Is.SameAs(expectedException));
    }
}
