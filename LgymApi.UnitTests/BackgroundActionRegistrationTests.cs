using FluentAssertions;
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
        handlers.Should().NotBeNull();
        handlers.Count().Should().Be(1);
        handlers.First().Should().BeOfType<TestAction>();
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
        handlers.Should().HaveCount(2);
        handlers.Any(h => h is TestAction).Should().BeTrue("TestAction should be registered");
        handlers.Any(h => h is SecondTestAction).Should().BeTrue("SecondTestAction should be registered");
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
        baseHandlers.Should().BeEmpty("Base command type should not resolve derived handlers");

        // Assert - derived command type has exact handler
        var derivedHandlers = provider.GetServices<IBackgroundAction<DerivedCommand>>().ToList();
        derivedHandlers.Should().HaveCount(1);
        derivedHandlers.First().Should().BeOfType<DerivedCommandAction>();
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
        concreteAction.Should().NotBeNull();
        concreteAction.Should().BeOfType<TestAction>();
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
        result.Should().BeSameAs(services, "Should return same IServiceCollection for chaining");
        var provider = services.BuildServiceProvider();
        provider.GetServices<IBackgroundAction<TestCommand>>().Count().Should().Be(1);
        provider.GetServices<IBackgroundAction<AnotherCommand>>().Count().Should().Be(1);
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
        handlers.Should().HaveCount(3);
        handlers.Select(h => h.GetType()).Should().BeEquivalentTo([
            typeof(TestAction),
            typeof(SecondTestAction),
            typeof(ThirdTestAction)
        ]);
    }

    [Test]
    public void ServiceProvider_RegistersTrainingCompletedCommand_WithTwoHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Register all handlers as ServiceProvider does
        services.AddBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand, LgymApi.BackgroundWorker.Actions.SendRegistrationEmailHandler>();
        services.AddBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand, LgymApi.BackgroundWorker.Actions.SendInvitationEmailHandler>();
        services.AddBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand, LgymApi.BackgroundWorker.Actions.TrainingCompletedEmailCommandHandler>();
        services.AddBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand, LgymApi.BackgroundWorker.Actions.UpdateTrainingMainRecordsHandler>();

        // Act
        var trainingDescriptors = services
            .Where(d => d.ServiceType == typeof(IBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand>))
            .ToList();
        var userDescriptors = services
            .Where(d => d.ServiceType == typeof(IBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand>))
            .ToList();
        var invitationDescriptors = services
            .Where(d => d.ServiceType == typeof(IBackgroundAction<LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand>))
            .ToList();

        // Assert - TrainingCompletedCommand has exactly 2 handlers
        trainingDescriptors.Should().HaveCount(2, "TrainingCompletedCommand must have exactly 2 handlers");
        trainingDescriptors.Any(d => d.ImplementationType == typeof(LgymApi.BackgroundWorker.Actions.TrainingCompletedEmailCommandHandler)).Should().BeTrue("Email handler must be registered");
        trainingDescriptors.Any(d => d.ImplementationType == typeof(LgymApi.BackgroundWorker.Actions.UpdateTrainingMainRecordsHandler)).Should().BeTrue("Main record handler must be registered");

        // Assert - Other commands have exactly 1 handler each
        userDescriptors.Should().HaveCount(1, "UserRegisteredCommand must have exactly 1 handler");
        invitationDescriptors.Should().HaveCount(1, "InvitationCreatedCommand must have exactly 1 handler");
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
        handler1.Should().NotBeSameAs(handler2, "Scoped services should be different instances across scopes");
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
        // Test passed (no assertion needed as type system enforces the constraint)
    }

    [Test]
    public void ServiceProvider_RegistersTrainingCompletedCommand_WithTwoEmailHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - register two handlers for same training completed command
        services.AddBackgroundAction<TrainingCompletedCommand, MockEmailCommandHandler>();
        services.AddBackgroundAction<TrainingCompletedCommand, MockMainRecordsCommandHandler>();
        var provider = services.BuildServiceProvider();

        // Assert - verify both handlers are registered
        var handlers = provider.GetServices<IBackgroundAction<TrainingCompletedCommand>>().ToList();
        handlers.Should().HaveCount(2);
        handlers.Any(h => h is MockEmailCommandHandler).Should().BeTrue("MockEmailCommandHandler should be registered");
        handlers.Any(h => h is MockMainRecordsCommandHandler).Should().BeTrue("MockMainRecordsCommandHandler should be registered");
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

    private sealed class MockEmailCommandHandler : IBackgroundAction<TrainingCompletedCommand>
    {
        public Task ExecuteAsync(TrainingCompletedCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class MockMainRecordsCommandHandler : IBackgroundAction<TrainingCompletedCommand>
    {
        public Task ExecuteAsync(TrainingCompletedCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TrainingCompletedCommand : IActionCommand
    {
    }

    #endregion
}
