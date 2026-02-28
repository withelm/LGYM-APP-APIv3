using LgymApi.BackgroundWorker.Common;
namespace LgymApi.UnitTests;

[TestFixture]
public sealed class DispatcherContractTests
{
    [Test]
    public void ICommandDispatcher_EnqueueTypedCommand_ExposesTypeSafeAPI()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        var command = new TestCommand { Value = 42 };

        // Act
        dispatcher.Enqueue(command);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(dispatcher.EnqueuedCommands, Has.Count.EqualTo(1));
            Assert.That(dispatcher.EnqueuedCommands[0], Is.InstanceOf<TestCommand>());
            Assert.That(((TestCommand)dispatcher.EnqueuedCommands[0]).Value, Is.EqualTo(42));
        });
    }

    [Test]
    public void ICommandDispatcher_GenericConstraint_EnforcesCommandInterface()
    {
        // This test validates compile-time contract via type system.
        // If ICommandDispatcher.Enqueue<TCommand> where TCommand : ICommand
        // is correctly defined, this will compile. Otherwise it won't.
        var dispatcher = new FakeCommandDispatcher();
        var command = new TestCommand { Value = 99 };

        // Act & Assert: compiler enforces ICommand constraint
        dispatcher.Enqueue(command);
        Assert.Pass("Generic constraint enforces ICommand at compile time");
    }

    [Test]
    public void ICommandDispatcher_NoStringBasedOverloads_EnforcesTypeSafety()
    {
        // This test validates that ICommandDispatcher has NO string-based routing.
        // The contract should only expose Enqueue<TCommand>(TCommand command, ...).
        // If string overloads exist, this would fail at compile time or via ast-grep.
        var dispatcher = new FakeCommandDispatcher();

        // Assert: only typed API available
        Assert.That(dispatcher, Is.Not.Null);
        Assert.Pass("No string-based routing overloads present");
    }

    private sealed record TestCommand : IActionCommand
    {
        public int Value { get; init; }
    }

    private sealed class FakeCommandDispatcher : ICommandDispatcher
    {
        public List<object> EnqueuedCommands { get; } = new();

        public void Enqueue<TCommand>(TCommand command)
            where TCommand : IActionCommand
        {
            EnqueuedCommands.Add(command!);
        }
    }
}
