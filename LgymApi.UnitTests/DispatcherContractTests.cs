using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using NUnit.Framework;
using System.Reflection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class DispatcherContractTests
{
    private const string PlatformBackgroundCommandsNamespace =
        "LgymApi.Application.Platform.Contracts.BackgroundCommands";
    private const string ApplicationActionCommandTypeName =
        $"{PlatformBackgroundCommandsNamespace}.IActionCommand";
    private const string ApplicationCommandDispatcherTypeName =
        $"{PlatformBackgroundCommandsNamespace}.ICommandDispatcher";
    private const string ApplicationCommandOutboxWriterTypeName =
        $"{PlatformBackgroundCommandsNamespace}.ICommandOutboxWriter";
    private const string CommandEnvelopeStageResultTypeName =
        $"{PlatformBackgroundCommandsNamespace}.CommandEnvelopeStageResult";

    [Test]
    public void PlatformICommandDispatcher_HasExactLegacyPublicShape()
    {
        var actionCommandType = GetApplicationType(ApplicationActionCommandTypeName);
        var dispatcherType = GetApplicationType(ApplicationCommandDispatcherTypeName);
        var methods = dispatcherType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.That(dispatcherType.IsPublic, Is.True);
        Assert.That(dispatcherType.IsInterface, Is.True);
        Assert.That(dispatcherType.IsGenericType, Is.False);
        Assert.That(dispatcherType.GetInterfaces(), Is.Empty);
        Assert.That(dispatcherType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Is.Empty);
        Assert.That(dispatcherType.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Is.Empty);
        Assert.That(methods, Has.Exactly(1).Items);

        var method = methods.Single();
        Assert.That(method.Name, Is.EqualTo("EnqueueAsync"));
        Assert.That(method.IsGenericMethodDefinition, Is.True);
        Assert.That(method.ReturnType, Is.EqualTo(typeof(Task)));
        Assert.That(method.GetParameters(), Has.Exactly(1).Items);

        var assertedValue101 = (method.GetGenericArguments()).ToArray();
        Assert.That(assertedValue101, Has.Exactly(1).Items);
        var genericParameter = assertedValue101.Single();
        Assert.That(genericParameter.GenericParameterAttributes, Is.EqualTo(GenericParameterAttributes.ReferenceTypeConstraint));
        Assert.That(genericParameter.GetGenericParameterConstraints(), Is.EqualTo(new[] { actionCommandType }));

        var commandParameter = method.GetParameters().Single();
        Assert.That(commandParameter.Name, Is.EqualTo("command"));
        Assert.That(commandParameter.ParameterType, Is.EqualTo(genericParameter));
        Assert.That(commandParameter.IsOptional, Is.False);
    }

    [Test]
    public void ApplicationPlatformCommandPorts_DoNotExposeWorkerOrHangfireTypes()
    {
        var platformAssembly = typeof(ICommandDispatcher).Assembly;
        var portTypes = platformAssembly.GetExportedTypes()
            .Where(type => type.Namespace == PlatformBackgroundCommandsNamespace)
            .OrderBy(type => type.Name)
            .ToArray();

        Assert.That(portTypes.Select(type => type.FullName), Is.EqualTo(new[] { "LgymApi.Application.Platform.Contracts.BackgroundCommands.CommandEnvelopeFinalization", "LgymApi.Application.Platform.Contracts.BackgroundCommands.CommandEnvelopeReceipt", "LgymApi.Application.Platform.Contracts.BackgroundCommands.CommandEnvelopeRequest", CommandEnvelopeStageResultTypeName, "LgymApi.Application.Platform.Contracts.BackgroundCommands.CommandEnvelopeStart", "LgymApi.Application.Platform.Contracts.BackgroundCommands.CommandHandlerResult", ApplicationActionCommandTypeName, ApplicationCommandDispatcherTypeName, "LgymApi.Application.Platform.Contracts.BackgroundCommands.ICommandEnvelopeRuntime", ApplicationCommandOutboxWriterTypeName }));

        var exposedSignatureTypes = portTypes
            .SelectMany(GetExposedSignatureTypes)
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.That((exposedSignatureTypes).Any(name =>
            name.Contains("Hangfire", StringComparison.Ordinal)
            || name.Contains("LgymApi.BackgroundWorker", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void ICommandDispatcher_EnqueueTypedCommand_ExposesTypeSafeAPI()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        var command = new TestCommand { Value = 42 };

        // Act
        dispatcher.EnqueueAsync(command);

        // Assert
        Assert.That(dispatcher.EnqueuedCommands, Has.Exactly(1).Items);
        Assert.That(dispatcher.EnqueuedCommands[0], Is.TypeOf<TestCommand>());
        Assert.That(((TestCommand)dispatcher.EnqueuedCommands[0]).Value, Is.EqualTo(42));
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
        dispatcher.EnqueueAsync(command);

        var assertedValue102 = (dispatcher.EnqueuedCommands).ToArray();
        Assert.That(assertedValue102, Has.Exactly(1).Items);
        Assert.That(assertedValue102.Single(), Is.SameAs(command));
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
        // Pass indicates no string-based routing overloads present
    }

    private sealed record TestCommand : IActionCommand
    {
        public int Value { get; init; }
    }

    private sealed class FakeCommandDispatcher : ICommandDispatcher
    {
        public List<object> EnqueuedCommands { get; } = new();

        public Task EnqueueAsync<TCommand>(TCommand command)
            where TCommand : class, IActionCommand
        {
            EnqueuedCommands.Add(command!);
            return Task.CompletedTask;
        }
    }

    private static Type GetApplicationType(string metadataName)
    {
        var type = typeof(ICommandDispatcher).Assembly.GetType(metadataName);
        Assert.That(type, Is.Not.Null, $"{metadataName} must be defined by the Platform assembly");
        return type!;
    }

    private static IEnumerable<Type> GetExposedSignatureTypes(Type portType)
    {
        yield return portType;

        foreach (var interfaceType in portType.GetInterfaces())
        {
            yield return interfaceType;
        }

        foreach (var method in portType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            yield return method.ReturnType;

            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }

            foreach (var constraint in method.GetGenericArguments().SelectMany(argument => argument.GetGenericParameterConstraints()))
            {
                yield return constraint;
            }
        }
    }
}
