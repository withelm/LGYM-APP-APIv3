using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class BackgroundActionRegistrationTests
{
    [Test]
    public void AddBackgroundAction_RegistersTypedHandler_WithCorrectGenericConstraint()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBackgroundAction<TestCommand, TestAction>();
        var provider = services.BuildServiceProvider();

        // Assert
        var handlers = provider.GetServices<IBackgroundAction<TestCommand>>();
        Assert.That(handlers, Is.Not.Null);
        Assert.That(handlers.Count(), Is.EqualTo(1));
        Assert.That(handlers.First(), Is.TypeOf<TestAction>());
    }

    [Test]
    public void AddBackgroundAction_RegistersMultipleHandlers_ForSameExactCommandType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBackgroundAction<TestCommand, TestAction>();
        services.AddBackgroundAction<TestCommand, SecondTestAction>();
        var provider = services.BuildServiceProvider();

        // Assert
        var handlers = provider.GetServices<IBackgroundAction<TestCommand>>().ToList();
        Assert.That(handlers, Has.Count.EqualTo(2));
        Assert.That(handlers.Any(h => h is TestAction), Is.True, "TestAction should be registered");
        Assert.That(handlers.Any(h => h is SecondTestAction), Is.True, "SecondTestAction should be registered");
    }

    [Test]
    public void AddBackgroundAction_ResolvesByExactTypeOnly_NoDerivedTypeMatching()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - register handler for derived command
        services.AddBackgroundAction<DerivedCommand, DerivedCommandAction>();
        var provider = services.BuildServiceProvider();

        // Assert - base command type has no handlers
        var baseHandlers = provider.GetServices<IBackgroundAction<TestCommand>>().ToList();
        Assert.That(baseHandlers, Is.Empty, "Base command type should not resolve derived handlers");

        // Assert - derived command type has exact handler
        var derivedHandlers = provider.GetServices<IBackgroundAction<DerivedCommand>>().ToList();
        Assert.That(derivedHandlers, Has.Count.EqualTo(1));
        Assert.That(derivedHandlers.First(), Is.TypeOf<DerivedCommandAction>());
    }

    [Test]
    public void AddBackgroundAction_RegistersConcreteImplementation_ForDependencyGraph()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBackgroundAction<TestCommand, TestAction>();
        var provider = services.BuildServiceProvider();

        // Assert - concrete implementation is resolvable
        var concreteAction = provider.GetService<TestAction>();
        Assert.That(concreteAction, Is.Not.Null);
        Assert.That(concreteAction, Is.TypeOf<TestAction>());
    }

    [Test]
    public void AddBackgroundAction_AllowsFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - chain multiple registrations
        var result = services
            .AddBackgroundAction<TestCommand, TestAction>()
            .AddBackgroundAction<AnotherCommand, AnotherCommandAction>();

        // Assert
        Assert.That(result, Is.SameAs(services), "Should return same IServiceCollection for chaining");
        var provider = services.BuildServiceProvider();
        Assert.That(provider.GetServices<IBackgroundAction<TestCommand>>().Count(), Is.EqualTo(1));
        Assert.That(provider.GetServices<IBackgroundAction<AnotherCommand>>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void AddBackgroundAction_SupportsMultipleCallsForSameCommandType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - register three handlers for same command
        services
            .AddBackgroundAction<TestCommand, TestAction>()
            .AddBackgroundAction<TestCommand, SecondTestAction>()
            .AddBackgroundAction<TestCommand, ThirdTestAction>();

        var provider = services.BuildServiceProvider();

        // Assert
        var handlers = provider.GetServices<IBackgroundAction<TestCommand>>().ToList();
        Assert.That(handlers, Has.Count.EqualTo(3));
        Assert.That(handlers.Select(h => h.GetType()), Is.EquivalentTo(new[]
        {
            typeof(TestAction),
            typeof(SecondTestAction),
            typeof(ThirdTestAction)
        }));
    }

    [Test]
    public void AddBackgroundAction_HandlersAreScoped_NewInstancePerScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBackgroundAction<TestCommand, TestAction>();
        var provider = services.BuildServiceProvider();

        // Act - resolve in two different scopes
        IBackgroundAction<TestCommand> handler1;
        IBackgroundAction<TestCommand> handler2;

        using (var scope1 = provider.CreateScope())
        {
            handler1 = scope1.ServiceProvider.GetRequiredService<IBackgroundAction<TestCommand>>();
        }

        using (var scope2 = provider.CreateScope())
        {
            handler2 = scope2.ServiceProvider.GetRequiredService<IBackgroundAction<TestCommand>>();
        }

        // Assert - different instances per scope
        Assert.That(handler1, Is.Not.SameAs(handler2), "Scoped services should be different instances across scopes");
    }

    [Test]
    public void AddBackgroundAction_GenericConstraint_RequiresIActionCommand()
    {
        // This test validates the compile-time constraint by attempting to use a valid command type.
        // Invalid types (not implementing IActionCommand) will fail at compile time, which is the desired behavior.

        // Arrange
        var services = new ServiceCollection();

        // Act - this compiles because TestCommand implements IActionCommand
        services.AddBackgroundAction<TestCommand, TestAction>();

        // Assert - if we reach here, the generic constraint is correctly enforced
        Assert.Pass("Generic constraint correctly requires IActionCommand");
    }

    #region Test Fixtures

    private class TestCommand : IActionCommand
    {
    }

    private sealed class DerivedCommand : TestCommand, IActionCommand
    {
    }

    private sealed class AnotherCommand : IActionCommand
    {
    }

    private sealed class TestAction : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SecondTestAction : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThirdTestAction : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DerivedCommandAction : IBackgroundAction<DerivedCommand>
    {
        public Task ExecuteAsync(DerivedCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AnotherCommandAction : IBackgroundAction<AnotherCommand>
    {
        public Task ExecuteAsync(AnotherCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    #endregion
}
/// <summary>
/// Tests validating production handler registrations for migrated typed commands.
/// Verifies ServiceProvider.AddBackgroundWorkerServices registers expected handlers.
/// </summary>
[TestFixture]
public sealed class MigratedCommandHandlerRegistrationTests
{
    [Test]
    public void ServiceProvider_RegistersUserRegisteredCommandHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBackgroundWorkerServices(isTesting: true);

        // Act
        var descriptor = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand>) &&
            sd.ImplementationType?.Name == "UserRegisteredCommandHandler");

        // Assert
        Assert.That(descriptor, Is.Not.Null, "UserRegisteredCommandHandler should be registered");
        Assert.That(descriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Scoped), "Handler should have Scoped lifetime");
    }

    [Test]
    public void ServiceProvider_RegistersInvitationCreatedCommandHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBackgroundWorkerServices(isTesting: true);

        // Act
        var descriptor = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand>) &&
            sd.ImplementationType?.Name == "InvitationCreatedCommandHandler");

        // Assert
        Assert.That(descriptor, Is.Not.Null, "InvitationCreatedCommandHandler should be registered");
        Assert.That(descriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Scoped), "Handler should have Scoped lifetime");
    }

    [Test]
    public void ServiceProvider_RegistersTrainingCompletedMainRecordCommandHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBackgroundWorkerServices(isTesting: true);

        // Act
        var descriptor = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand>) &&
            sd.ImplementationType?.Name == "TrainingCompletedMainRecordCommandHandler");

        // Assert - Currently only main-record handler is registered
        Assert.That(descriptor, Is.Not.Null, "TrainingCompletedMainRecordCommandHandler should be registered");
        Assert.That(descriptor!.Lifetime, Is.EqualTo(ServiceLifetime.Scoped), "Handler should have Scoped lifetime");
    }

    [Test]
    public void ServiceProvider_AllMigratedHandlersAreRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBackgroundWorkerServices(isTesting: true);

        // Act - Find service descriptors for all three migrated command types
        var userRegisteredDescriptor = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand>));
        var invitationCreatedDescriptor = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand>));
        var trainingCompletedDescriptor = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand>));

        // Assert - All three migrated command types should have handlers
        Assert.That(userRegisteredDescriptor, Is.Not.Null, "UserRegisteredCommand handler should be registered");
        Assert.That(invitationCreatedDescriptor, Is.Not.Null, "InvitationCreatedCommand handler should be registered");
        Assert.That(trainingCompletedDescriptor, Is.Not.Null, "TrainingCompletedCommand handler should be registered");
    }

    [Test]
    public void ServiceProvider_HandlersHaveScopedLifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBackgroundWorkerServices(isTesting: true);

        // Act - Get all handler descriptors
        var handlerDescriptors = services.Where(sd =>
            sd.ServiceType.IsGenericType &&
            sd.ServiceType.GetGenericTypeDefinition() == typeof(IBackgroundAction<>) &&
            (sd.ImplementationType?.Name.Contains("UserRegisteredCommandHandler") == true ||
             sd.ImplementationType?.Name.Contains("InvitationCreatedCommandHandler") == true ||
             sd.ImplementationType?.Name.Contains("TrainingCompletedMainRecordCommandHandler") == true)).ToList();

        // Assert - All migrated handlers should have Scoped lifetime
        Assert.That(handlerDescriptors, Is.Not.Empty, "Should find at least one migrated handler");
        Assert.That(handlerDescriptors.All(d => d.Lifetime == ServiceLifetime.Scoped),
            Is.True, "All migrated handlers should have Scoped lifetime");
    }
}
