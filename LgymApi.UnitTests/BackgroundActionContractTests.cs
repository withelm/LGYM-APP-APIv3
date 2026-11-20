using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker.Actions.Contracts;
using NUnit.Framework;
using System.Reflection;

[TestFixture]
public sealed class BackgroundActionContractTests
{
    private const string ApplicationActionCommandTypeName =
        "LgymApi.Application.Platform.Contracts.BackgroundCommands.IActionCommand";

    [Test]
    public void ApplicationIActionCommand_IsExactPublicMarkerInterface()
    {
        var type = GetPlatformType(ApplicationActionCommandTypeName);

        Assert.That(type.IsPublic, Is.True);
        Assert.That(type.IsInterface, Is.True);
        Assert.That(type.IsGenericType, Is.False);
        Assert.That(type.GetInterfaces(), Is.Empty);
        Assert.That(type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly), Is.Empty);
    }

    [Test]
    public void IActionCommand_IsMarkerInterface()
    {
        // Verify IActionCommand is defined and can be implemented
        var type = typeof(IActionCommand);
        Assert.That(type.IsInterface, Is.True);
        Assert.That(type.GetProperties(), Is.Empty);
        Assert.That(type.GetMethods(), Has.Exactly(0).Items);
    }

    [Test]
    public void IBackgroundAction_HasGenericConstraint()
    {
        // Verify IBackgroundAction<TCommand> requires TCommand to implement IActionCommand
        var type = typeof(IBackgroundAction<>);
        var typeParams = type.GetGenericArguments();

        Assert.That(typeParams, Has.Exactly(1).Items);
        var constraint = typeParams[0].GetGenericParameterConstraints();
        Assert.That(constraint, Does.Contain(typeof(IActionCommand)));
    }

    [Test]
    public void IBackgroundAction_HasExecuteAsyncMethod()
    {
        // Verify ExecuteAsync method signature
        var type = typeof(IBackgroundAction<>);
        var method = type.GetMethod("ExecuteAsync");

        Assert.That(method, Is.Not.Null);
        Assert.That(method.ReturnType, Is.EqualTo(typeof(Task)));

        var parameters = method.GetParameters();
        Assert.That(parameters, Has.Exactly(2).Items);
        Assert.That(parameters[0].Name, Is.EqualTo("command"));
        Assert.That(parameters[1].Name, Is.EqualTo("cancellationToken"));
        Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(CancellationToken)));
    }

    [Test]
    public void Implementation_CanImplementIActionCommand()
    {
        // Arrange & Act
        var command = new TestCommand();

        // Assert
        Assert.That(command, Is.InstanceOf<IActionCommand>());
    }

    [Test]
    public void Implementation_CanImplementIBackgroundAction()
    {
        // Arrange & Act
        var handler = new TestActionHandler();

        // Assert
        Assert.That(handler, Is.InstanceOf<IBackgroundAction<TestCommand>>());
    }

    [Test]
    public async Task Implementation_ExecuteAsync_WorksWithCancellationToken()
    {
        // Arrange
        var handler = new TestActionHandler();
        var command = new TestCommand();
        using var cts = new CancellationTokenSource();

        // Act
        await handler.ExecuteAsync(command, cts.Token);

        // Assert
        Assert.That(handler.WasExecuted, Is.True);
        Assert.That(handler.ReceivedCommand, Is.SameAs(command));
    }

    [Test]
    public async Task Implementation_ExecuteAsync_WithDefaultCancellationToken()
    {
        // Arrange
        var handler = new TestActionHandler();
        var command = new TestCommand();

        // Act
        await handler.ExecuteAsync(command);

        // Assert
        Assert.That(handler.WasExecuted, Is.True);
    }

    [Test]
    public void MultipleHandlers_CanCoexist_ForDifferentCommands()
    {
        // Verify strong typing prevents mixing incompatible handlers and commands
        var testHandler = new TestActionHandler();
        var anotherHandler = new AnotherTestActionHandler();

        Assert.That(testHandler, Is.InstanceOf<IBackgroundAction<TestCommand>>());
        Assert.That(anotherHandler, Is.InstanceOf<IBackgroundAction<AnotherTestCommand>>());
    }

    [Test]
    public void GenericConstraint_PreventsNonActionCommandTypes()
    {
        // Compile-time check: IBackgroundAction<T> where T : IActionCommand
        // This test verifies the constraint is properly defined at runtime
        var type = typeof(IBackgroundAction<>);
        var constraint = type.GetGenericArguments()[0].GetGenericParameterConstraints();

        Assert.That(constraint, Does.Contain(typeof(IActionCommand)));
    }

    // Test implementations
    private sealed class TestCommand : IActionCommand
    {
    }

    private sealed class AnotherTestCommand : IActionCommand
    {
    }

    private sealed class TestActionHandler : IBackgroundAction<TestCommand>
    {
        public bool WasExecuted { get; private set; }
        public TestCommand? ReceivedCommand { get; private set; }

        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            ReceivedCommand = command;
            return Task.CompletedTask;
        }
    }

    private sealed class AnotherTestActionHandler : IBackgroundAction<AnotherTestCommand>
    {
        public Task ExecuteAsync(AnotherTestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static Type GetPlatformType(string metadataName)
    {
        var type = typeof(IActionCommand).Assembly.GetType(metadataName);
        Assert.That(type, Is.Not.Null, $"{metadataName} must be defined by the Platform assembly");
        return type!;
    }
}
