using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ApplicationActionCommand = LgymApi.Application.Platform.Contracts.BackgroundCommands.IActionCommand;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CommandContractRegistryTests
{
    [Test]
    public void CreateDefault_ContainsTheLegacyCommandsAndNewReportingCommands()
    {
        var registry = CommandContractRegistry.CreateDefault();

        Assert.That(registry.Contracts, Has.Exactly(19).Items);
        Assert.That(registry.Contracts.Sum(contract => contract.ExpectedHandlerTypes.Count), Is.EqualTo(20));

        foreach (var legacyContract in LegacyCommandContractManifest.All)
        {
            var contract = registry.Contracts.Single(candidate =>
                candidate.CanonicalId == legacyContract.CanonicalId);

            Assert.That(contract.RuntimeType.FullName, Is.EqualTo(legacyContract.FutureClrNameReadAlias));
            Assert.That(contract.ReadAlias, Is.EqualTo(legacyContract.FutureClrNameReadAlias));
            Assert.That(contract.ExpectedHandlerTypes.Select(type => type.FullName), Is.EqualTo(legacyContract.HandlerTypeFullNames));
        }

        Assert.That(registry.Contracts.Select(contract => contract.RuntimeType), Is.SupersetOf(LegacyCommandContractManifest.ExpectedRuntimeTypes));
    }

    [TestCaseSource(typeof(LegacyCommandContractManifest), nameof(LegacyCommandContractManifest.CommandCases))]
    public void DefaultRegistry_ResolvesCanonicalIdsAndReadAliasesButWritesOnlyCanonicalIds(
        LegacyCommandContract legacyContract)
    {
        var registry = CommandContractRegistry.CreateDefault();
        var runtimeType = legacyContract.CommandType;

        var writeDescriptor = registry.DescribeForWrite(runtimeType);
        var canonicalReadDescriptor = registry.Resolve(legacyContract.CanonicalId);
        var aliasReadDescriptor = registry.Resolve(legacyContract.FutureClrNameReadAlias);

        var assertedChain35 = writeDescriptor.CanonicalId;
        Assert.That(assertedChain35, Is.EqualTo(legacyContract.CanonicalId));
        Assert.That(assertedChain35, Is.Not.EqualTo(legacyContract.FutureClrNameReadAlias));
        Assert.That(canonicalReadDescriptor.RuntimeType, Is.EqualTo(runtimeType));
        Assert.That(aliasReadDescriptor.RuntimeType, Is.EqualTo(runtimeType));
        Assert.That(aliasReadDescriptor.CanonicalId, Is.EqualTo(legacyContract.CanonicalId));
    }

    [Test]
    public void CreateForTesting_RejectsInvalidRows()
    {
        var rows = CreateValidRows();

        AssertInvalid(
            [rows[0], rows[1] with { CanonicalId = rows[0].CanonicalId }],
            "Canonical command IDs must be unique.");
        AssertInvalid(
            [rows[0], rows[1] with { ReadAlias = rows[0].ReadAlias }],
            "Command read aliases must be unique.");
        AssertInvalid(
            [rows[0], rows[1] with { RuntimeType = rows[0].RuntimeType }],
            "Runtime command types must be unique.");
        AssertInvalid(
            [rows[0], rows[1] with { CanonicalId = rows[0].ReadAlias }],
            "Canonical command IDs and read aliases must not overlap.");
        AssertInvalid(
            [rows[0] with { CanonicalId = "" }],
            "Canonical command IDs must not be empty.");
        AssertInvalid(
            [rows[0] with { ReadAlias = " " }],
            "Command read aliases must not be empty.");
        AssertInvalid(
            [rows[0] with { RuntimeType = typeof(string), ReadAlias = typeof(string).FullName! }],
            "Runtime type 'System.String' must implement IActionCommand.");
        AssertInvalid(
            [rows[0] with { ReadAlias = "Tests.Commands.WrongAlias" }],
            $"Read alias for '{rows[0].CanonicalId}' must equal its runtime CLR FullName.");
        AssertInvalid(
            [rows[0] with { ExpectedHandlerTypes = [] }],
            $"Command '{rows[0].CanonicalId}' must declare at least one expected handler.");
        AssertInvalid(
            [rows[0] with { ExpectedHandlerTypes = [typeof(SecondHandler)] }],
            $"Handler metadata for '{rows[0].CanonicalId}' does not target its runtime command.");
        AssertInvalid(
            [rows[0] with { ExpectedHandlerTypes = [typeof(FirstHandler), typeof(FirstHandler)] }],
            $"Expected handlers for '{rows[0].CanonicalId}' must be unique.");
    }

    [Test]
    public void CreateForTesting_RejectsHandlerWhoseRuntimeNameCollidesWithCanonicalId()
    {
        var collision = new CommandContract(
            typeof(CanonicalNameCollisionCommand).FullName!,
            typeof(FirstCommand),
            typeof(FirstCommand).FullName!,
            [typeof(CanonicalNameCollisionHandler)]);

        AssertInvalid(
            [collision],
            $"Handler metadata for '{collision.CanonicalId}' does not target its runtime command.");
    }

    [Test]
    public void CreateForTesting_RejectsMissingExtraAndChangedDefaultMetadata()
    {
        var rows = CommandContractRegistry.CreateDefault().Contracts.ToArray();

        AssertInvalidDefault(
            rows[..^1],
            "The default command registry must contain exactly 19 rows.");
        AssertInvalidDefault(
            rows.Append(CreateValidRows()[0]).ToArray(),
            "The default command registry must contain exactly 19 rows.");
        AssertInvalidDefault(
            ReplaceFirst(rows, rows[0] with
            {
                CanonicalId = "Unexpected.CanonicalId",
                RuntimeType = typeof(FirstCommand),
                ReadAlias = typeof(FirstCommand).FullName!,
                ExpectedHandlerTypes = [typeof(FirstHandler)]
            }),
            "Default canonical command IDs must match the fixed 19-command contract.");
        AssertInvalidDefault(
            ReplaceFirst(rows, rows[0] with
            {
                RuntimeType = typeof(FirstCommand),
                ReadAlias = typeof(FirstCommand).FullName!,
                ExpectedHandlerTypes = [typeof(FirstHandler)]
            }),
            $"Default runtime metadata mismatch for '{rows[0].CanonicalId}'.");
        AssertInvalidDefault(
            ReplaceFirst(rows, rows[0] with
            {
                ExpectedHandlerTypes = [typeof(SecondHandler)]
            }),
            $"Handler metadata for '{rows[0].CanonicalId}' does not target its runtime command.");
        AssertInvalidDefault(
            ReplaceFirst(rows, rows[0] with
            {
                ExpectedHandlerTypes =
                [
                    .. rows[0].ExpectedHandlerTypes,
                    typeof(AlternateUserRegisteredHandler)
                ]
            }),
            "The default command registry must declare exactly 20 handlers.");

        var trainingCompleted = rows.Single(row => row.CanonicalId.EndsWith(
            ".TrainingCompletedCommand",
            StringComparison.Ordinal));
        AssertInvalidDefault(
            rows.Select(row => row == trainingCompleted
                ? trainingCompleted with { ExpectedHandlerTypes = [trainingCompleted.ExpectedHandlerTypes[0]] }
                : row).ToArray(),
            "The default command registry must declare exactly 20 handlers.");
        AssertInvalidDefault(
            rows.Select(row => row == trainingCompleted
                ? trainingCompleted with
                {
                    ExpectedHandlerTypes =
                    [
                        .. trainingCompleted.ExpectedHandlerTypes,
                        typeof(AlternateTrainingCompletedHandler)
                    ]
                }
                : row).ToArray(),
            "The default command registry must declare exactly 20 handlers.");

        var changedTrainingHandlers = trainingCompleted with
        {
            ExpectedHandlerTypes =
            [
                trainingCompleted.ExpectedHandlerTypes[0],
                typeof(AlternateTrainingCompletedHandler)
            ]
        };
        AssertInvalidDefault(
            rows.Select(row => row == trainingCompleted ? changedTrainingHandlers : row).ToArray(),
            $"Default handler metadata mismatch for '{trainingCompleted.CanonicalId}'.");
    }

    [Test]
    public void Resolve_RejectsUnknownDurableCommandId()
    {
        var registry = CommandContractRegistry.CreateDefault();

        var action = () => registry.Resolve("Unknown.Command");

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Unknown durable command identifier 'Unknown.Command'.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void AddBackgroundWorkerServices_RegistersOneDefaultRegistrySingleton()
    {
        var services = new ServiceCollection();
        services.AddBackgroundWorkerServices(isTesting: true);

        var descriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(CommandContractRegistry))
            .ToArray();

        Assert.That(descriptors, Has.Exactly(1).Items);
        Assert.That(descriptors[0].Lifetime, Is.EqualTo(ServiceLifetime.Singleton));

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<CommandContractRegistry>();
        var second = provider.GetRequiredService<CommandContractRegistry>();
        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void DispatcherAndOrchestrator_RequireTheInjectedRegistry()
    {
        var runtimeTypes = new[]
        {
            typeof(CommandDispatcher),
            typeof(BackgroundActionOrchestratorService)
        };

        foreach (var runtimeType in runtimeTypes)
        {
            Assert.That(runtimeType.GetConstructors(), Has.Exactly(1).Items);
            Assert.That(runtimeType.GetConstructors().Single().GetParameters()
                .Count(parameter => parameter.ParameterType == typeof(CommandContractRegistry)), Is.EqualTo(1));
        }
    }

    private static CommandContract[] CreateValidRows() =>
    [
        new(
            "Tests.Commands.First",
            typeof(FirstCommand),
            typeof(FirstCommand).FullName!,
            [typeof(FirstHandler)]),
        new(
            "Tests.Commands.Second",
            typeof(SecondCommand),
            typeof(SecondCommand).FullName!,
            [typeof(SecondHandler)])
    ];

    private static void AssertInvalid(IReadOnlyCollection<CommandContract> rows, string expectedMessage)
    {
        var action = () => CommandContractRegistry.CreateForTesting(rows);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape(expectedMessage).Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    private static void AssertInvalidDefault(
        IReadOnlyCollection<CommandContract> rows,
        string expectedMessage)
    {
        var action = () => CommandContractRegistry.CreateForTesting(rows, requireDefaultContract: true);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape(expectedMessage).Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    private static CommandContract[] ReplaceFirst(
        IReadOnlyList<CommandContract> rows,
        CommandContract replacement) =>
        rows.Skip(1).Prepend(replacement).ToArray();

    private sealed class FirstCommand : ApplicationActionCommand;

    private sealed class SecondCommand : ApplicationActionCommand;

    private sealed class FirstHandler : IBackgroundAction<FirstCommand>
    {
        public Task ExecuteAsync(FirstCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class SecondHandler : IBackgroundAction<SecondCommand>
    {
        public Task ExecuteAsync(SecondCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CanonicalNameCollisionCommand : ApplicationActionCommand;

    private sealed class CanonicalNameCollisionHandler : IBackgroundAction<CanonicalNameCollisionCommand>
    {
        public Task ExecuteAsync(
            CanonicalNameCollisionCommand command,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class AlternateUserRegisteredHandler :
        IBackgroundAction<global::LgymApi.Application.Identity.Contracts.BackgroundCommands.UserRegisteredCommand>
    {
        public Task ExecuteAsync(
            global::LgymApi.Application.Identity.Contracts.BackgroundCommands.UserRegisteredCommand command,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class AlternateTrainingCompletedHandler :
        IBackgroundAction<global::LgymApi.Application.WorkoutProgress.Contracts.BackgroundCommands.TrainingCompletedCommand>
    {
        public Task ExecuteAsync(
            global::LgymApi.Application.WorkoutProgress.Contracts.BackgroundCommands.TrainingCompletedCommand command,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
