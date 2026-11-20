using System.Reflection;
using System.Text.Json;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;
using LgymApi.Application.WorkoutProgress.Contracts.BackgroundActions;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using ApplicationActionCommand = LgymApi.Application.Platform.Contracts.BackgroundCommands.IActionCommand;
using IActionMessageScheduler = LgymApi.BackgroundWorker.Common.IActionMessageScheduler;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AcceptedProgressCommandOutboxTests
{
    private const string BackgroundCommandsNamespace =
        "LgymApi.Application.Platform.Contracts.BackgroundCommands";
    private const string CommandOutboxWriterTypeName =
        $"{BackgroundCommandsNamespace}.ICommandOutboxWriter";
    private const string CommandEnvelopeStageResultTypeName =
        $"{BackgroundCommandsNamespace}.CommandEnvelopeStageResult";
    private const string AcceptedProgressCommandTypeName =
        "LgymApi.Application.Reporting.Contracts.BackgroundCommands.ReportSubmissionAcceptedProgressCommand";
    private const string AcceptedProgressHandlerTypeName =
        "LgymApi.BackgroundWorker.Actions.ReportSubmissionAcceptedProgressCommandHandler";

    private Microsoft.Extensions.DependencyInjection.ServiceProvider? _serviceProvider;

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public void ICommandOutboxWriter_ExposesStageOnlyTypedContract()
    {
        var actionCommandType = typeof(IActionCommand);
        var writerType = GetRequiredPlatformType(
            CommandOutboxWriterTypeName,
            "T5 must add the Application-owned ICommandOutboxWriter port.");
        var resultType = GetRequiredPlatformType(
            CommandEnvelopeStageResultTypeName,
            "T5 must add CommandEnvelopeStageResult so callers can distinguish newly staged and existing envelopes.");

        Assert.That(writerType.IsInterface, Is.True);
        var method = writerType.GetMethod("StageAsync", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "ICommandOutboxWriter must expose StageAsync<TCommand>.");
        Assert.That(method!.IsGenericMethodDefinition, Is.True);
        Assert.That(method.GetParameters().Select(parameter => parameter.Name), Is.EqualTo(new[] { "command", "cancellationToken" }));
        Assert.That(method.GetParameters()[1].IsOptional, Is.True);

        var assertedValue76 = (method.GetGenericArguments()).ToArray();
        Assert.That(assertedValue76, Has.Exactly(1).Items);
        var genericParameter = assertedValue76.Single();
        Assert.That(genericParameter.GenericParameterAttributes, Is.EqualTo(GenericParameterAttributes.ReferenceTypeConstraint));
        Assert.That(genericParameter.GetGenericParameterConstraints(), Is.EqualTo(new[] { actionCommandType }));
        Assert.That(method.ReturnType, Is.EqualTo(typeof(Task<>).MakeGenericType(resultType)));
        Assert.That(resultType.GetProperty("EnvelopeId", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
        Assert.That(resultType.GetProperty("Envelope", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        Assert.That(resultType.GetProperty("WasExisting", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
    }

    [Test]
    public async Task StageAsync_StagesEnvelopeWithSharedSerializationAndNeverSavesOrSchedules()
    {
        var runtime = new FakeCommandEnvelopeRuntime();
        var unitOfWork = new FakeUnitOfWork();
        var scheduler = new FakeActionMessageScheduler();
        var command = new TestStageCommand { Value = "stage-only" };
        using var cancellationSource = new CancellationTokenSource();
        var writer = CreateWriter(
            runtime,
            unitOfWork,
            scheduler,
            includeHandler: true);

        var firstResult = await InvokeStageAsync(writer, command, cancellationSource.Token);
        var duplicateResult = await InvokeStageAsync(writer, command, cancellationSource.Token);

        Assert.That(runtime.StageInvocations, Has.Exactly(2).Items);
        Assert.That((runtime.StageInvocations).All(invocation =>
            invocation.Request.CommandId == "Tests.AcceptedProgress.StageCommand"
            && invocation.Request.PayloadJson == JsonSerializer.Serialize(command, SharedSerializationOptions.Current)
            && invocation.CancellationToken == cancellationSource.Token), Is.True);
        Assert.That(runtime.PersistInvocations, Is.Empty);
        Assert.That(unitOfWork.SaveCallCount, Is.EqualTo(0), "StageAsync must leave commit timing to its caller");
        Assert.That(scheduler.Enqueued, Is.Empty, "committed-intent dispatch must not run before the caller commits its UoW");
        Assert.That(GetEnvelopeId(firstResult), Is.EqualTo("stage-envelope-id"));
        Assert.That(GetEnvelopeId(duplicateResult), Is.EqualTo("stage-envelope-id"));
        Assert.That(GetWasExisting(firstResult), Is.False);
        Assert.That(GetWasExisting(duplicateResult), Is.True);
    }

    [Test]
    public async Task StageAsync_WhenNoExactHandlerExists_DoesNotStageSaveOrSchedule()
    {
        var runtime = new FakeCommandEnvelopeRuntime();
        var unitOfWork = new FakeUnitOfWork();
        var scheduler = new FakeActionMessageScheduler();
        using var cancellationSource = new CancellationTokenSource();
        var writer = CreateWriter(
            runtime,
            unitOfWork,
            scheduler,
            includeHandler: false);

        await InvokeStageAsync(writer, new TestStageCommand { Value = "no-handler" }, cancellationSource.Token);

        Assert.That(runtime.StageInvocations, Is.Empty, "StageAsync must validate exact handler availability before staging");
        Assert.That(runtime.PersistInvocations, Is.Empty);
        Assert.That(unitOfWork.SaveCallCount, Is.EqualTo(0));
        Assert.That(scheduler.Enqueued, Is.Empty);
    }

    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.Applied)]
    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.Duplicate)]
    public async Task AcceptedProgressCommandHandler_AppliedAndDuplicate_Complete(
        ReportSubmissionAcceptedProgressConsumeOutcome outcome)
    {
        var payload = CreateValidPayload();
        var port = Substitute.For<IReportSubmissionAcceptedProgressActionExecutionPort>();
        port.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var handler = CreateAcceptedProgressHandler(port);

        await ExecuteAcceptedProgressHandlerAsync(handler, payload);

        await port.Received(1).ExecuteAsync(
            JsonSerializer.Serialize(new ReportSubmissionAcceptedProgressCommand { Event = payload }, SharedSerializationOptions.Current),
            Arg.Any<CancellationToken>());
    }

    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.Invalid)]
    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.UnsupportedSchema)]
    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.Poison)]
    public async Task AcceptedProgressCommandHandler_BoundedOwnerFailure_IsPropagated(
        ReportSubmissionAcceptedProgressConsumeOutcome outcome)
    {
        var port = Substitute.For<IReportSubmissionAcceptedProgressActionExecutionPort>();
        port.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException(
                $"Report submission accepted-progress command delivery failed with outcome {outcome}.")));
        var handler = CreateAcceptedProgressHandler(port);

        var action = () => ExecuteAcceptedProgressHandlerAsync(handler, CreateValidPayload());

        var exception = (await Assert.CatchAsync<InvalidOperationException>(async () => { await action(); }))!;
        Assert.That(exception.Message, Does.Contain(outcome.ToString()));
        Assert.That(exception.Message.Length, Is.LessThanOrEqualTo(256));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AcceptedProgressCommandHandler_InvalidOrUnsupportedPayload_IsForwardedToOwner(
        bool malformedMeasurement)
    {
        var payload = malformedMeasurement
            ? CreateValidPayload() with
            {
                Measurements = [new ReportSubmissionAcceptedProgressMeasurement(
                    BodyParts.Unknown,
                    101.5,
                    MeasurementUnits.Centimeters)]
            }
            : CreateValidPayload() with { SchemaVersion = 2 };
        var port = Substitute.For<IReportSubmissionAcceptedProgressActionExecutionPort>();
        var handler = CreateAcceptedProgressHandler(port);

        await ExecuteAcceptedProgressHandlerAsync(handler, payload);

        await port.Received(1).ExecuteAsync(
            JsonSerializer.Serialize(new ReportSubmissionAcceptedProgressCommand { Event = payload }, SharedSerializationOptions.Current),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AcceptedProgressCommandHandler_MissingPayload_IsForwardedToOwner()
    {
        var port = Substitute.For<IReportSubmissionAcceptedProgressActionExecutionPort>();
        var handler = CreateAcceptedProgressHandler(port);

        await ExecuteAcceptedProgressHandlerAsync(handler, null);

        await port.Received(1).ExecuteAsync(
            "{}",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AcceptedProgressCommandHandler_UnexpectedConsumerException_RemainsRetryable()
    {
        var port = Substitute.For<IReportSubmissionAcceptedProgressActionExecutionPort>();
        port.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(
                new TimeoutException("temporary consumer outage")));
        var handler = CreateAcceptedProgressHandler(port);

        var action = () => ExecuteAcceptedProgressHandlerAsync(handler, CreateValidPayload());

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<TimeoutException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("temporary consumer outage").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    private object CreateWriter(
        FakeCommandEnvelopeRuntime runtime,
        FakeUnitOfWork unitOfWork,
        FakeActionMessageScheduler scheduler,
        bool includeHandler)
    {
        var writerPort = GetRequiredPlatformType(
            CommandOutboxWriterTypeName,
            "T5 must add ICommandOutboxWriter before staging can be tested.");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICommandEnvelopeRuntime>(runtime);
        services.AddSingleton<IUnitOfWork>(unitOfWork);
        services.AddSingleton<IActionMessageScheduler>(scheduler);
        services.AddSingleton<CommandContractRegistry>(CommandContractRegistry.CreateForTesting(
        [
            new CommandContract(
                "Tests.AcceptedProgress.StageCommand",
                typeof(TestStageCommand),
                typeof(TestStageCommand).FullName!,
                [typeof(TestStageActionHandler)])
        ]));
        services.AddSingleton<IBackgroundActionResolver>(serviceProvider =>
            new BackgroundActionResolver(serviceProvider.GetRequiredService<IServiceScopeFactory>()));

        if (includeHandler)
        {
            services.AddScoped<IBackgroundAction<TestStageCommand>, TestStageActionHandler>();
        }

        _serviceProvider = services.BuildServiceProvider();
        var implementationTypes = typeof(CommandDispatcher).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && writerPort.IsAssignableFrom(type))
            .ToArray();
        Assert.That(implementationTypes, Has.Exactly(1).Items,
            "T5 must provide exactly one runtime implementation of ICommandOutboxWriter.");
        var implementationType = implementationTypes.Single();

        return ActivatorUtilities.CreateInstance(_serviceProvider, implementationType);
    }

    private object CreateAcceptedProgressHandler(IReportSubmissionAcceptedProgressActionExecutionPort port)
    {
        var handlerType = GetRequiredWorkerType(
            AcceptedProgressHandlerTypeName,
            "T9 must add ReportSubmissionAcceptedProgressCommandHandler.");
        var commandType = GetRequiredReportingType(
            AcceptedProgressCommandTypeName,
            "T6 must add the Reporting-owned ReportSubmissionAcceptedProgressCommand.");
        var actionInterface = typeof(IBackgroundAction<>).MakeGenericType(commandType);
        Assert.That(actionInterface.IsAssignableFrom(handlerType), Is.True);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(port);
        _serviceProvider = services.BuildServiceProvider();
        return ActivatorUtilities.CreateInstance(_serviceProvider, handlerType);
    }

    private static async Task<object> InvokeStageAsync(
        object writer,
        TestStageCommand command,
        CancellationToken cancellationToken)
    {
        var writerType = GetRequiredPlatformType(
            CommandOutboxWriterTypeName,
            "T5 must add ICommandOutboxWriter before StageAsync can be invoked.");
        var method = writerType.GetMethod("StageAsync", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new AssertionException("ICommandOutboxWriter must expose StageAsync<TCommand>.");
        var task = method.MakeGenericMethod(typeof(TestStageCommand))
            .Invoke(writer, [command, cancellationToken]) as Task;

        Assert.That(task, Is.Not.Null, "StageAsync must return a Task<CommandEnvelopeStageResult>.");
        await task!;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static async Task ExecuteAcceptedProgressHandlerAsync(
        object handler,
        ReportSubmissionAcceptedProgressPayload? payload)
    {
        var commandType = GetRequiredReportingType(
            AcceptedProgressCommandTypeName,
            "T6 must add ReportSubmissionAcceptedProgressCommand before the Worker handler can execute it.");
        var command = CreateAcceptedProgressCommand(commandType, payload);
        var actionInterface = typeof(IBackgroundAction<>).MakeGenericType(commandType);
        var executeMethod = actionInterface.GetMethod("ExecuteAsync")
            ?? throw new AssertionException("The accepted-progress handler must implement IBackgroundAction<TCommand>.");
        var task = executeMethod.Invoke(handler, [command, CancellationToken.None]) as Task;

        Assert.That(task, Is.Not.Null);
        await task!;
    }

    private static object CreateAcceptedProgressCommand(
        Type commandType,
        ReportSubmissionAcceptedProgressPayload? payload)
    {
        var command = Activator.CreateInstance(commandType)
            ?? throw new AssertionException("ReportSubmissionAcceptedProgressCommand must be instantiable.");
        var eventProperty = commandType.GetProperty("Event", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(eventProperty, Is.Not.Null, "ReportSubmissionAcceptedProgressCommand must carry its nested accepted-progress Event.");
        Assert.That(eventProperty!.PropertyType, Is.EqualTo(typeof(ReportSubmissionAcceptedProgressPayload)));
        eventProperty.SetValue(command, payload);
        return command;
    }

    private static string? GetEnvelopeId(object result)
    {
        return result.GetType().GetProperty("EnvelopeId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(result) as string;
    }

    private static bool GetWasExisting(object result)
    {
        var wasExisting = result.GetType().GetProperty("WasExisting", BindingFlags.Public | BindingFlags.Instance)?.GetValue(result);
        Assert.That(wasExisting, Is.TypeOf<bool>());
        return (bool)wasExisting!;
    }

    private static Type GetRequiredReportingType(string typeName, string because)
    {
        var type = typeof(LgymApi.Application.Reporting.ServiceCollectionExtensions).Assembly.GetType(typeName);
        Assert.That(type, Is.Not.Null, because);
        return type!;
    }

    private static Type GetRequiredPlatformType(string typeName, string because)
    {
        var type = typeof(IActionCommand).Assembly.GetType(typeName);
        Assert.That(type, Is.Not.Null, because);
        return type!;
    }

    private static Type GetRequiredWorkerType(string typeName, string because)
    {
        var type = typeof(CommandDispatcher).Assembly.GetType(typeName);
        Assert.That(type, Is.Not.Null, because);
        return type!;
    }

    private static ReportSubmissionAcceptedProgressPayload CreateValidPayload()
    {
        return new ReportSubmissionAcceptedProgressPayload(
            1,
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
            "00000000-0000-0000-0000-000000000003",
            "00000000-0000-0000-0000-000000000004",
            ParseId<AccountReference>("00000000-0000-0000-0000-000000000005"),
            new DateTimeOffset(2026, 7, 20, 8, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 20, 8, 31, 0, TimeSpan.Zero),
            [new ReportSubmissionAcceptedProgressMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters)]);
    }

    private static Id<TEntity> ParseId<TEntity>(string value)
        where TEntity : class
    {
        Assert.That(Id<TEntity>.TryParse(value, out var id), Is.True);
        return id;
    }

    private sealed record TestStageCommand : ApplicationActionCommand
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class TestStageActionHandler : IBackgroundAction<TestStageCommand>
    {
        public Task ExecuteAsync(TestStageCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeActionMessageScheduler : IActionMessageScheduler
    {
        public List<Id<CommandEnvelope>> Enqueued { get; } = [];

        public string? Enqueue(string actionMessageId)
        {
            if (!Id<CommandEnvelope>.TryParse(actionMessageId, out var parsedActionMessageId))
            {
                throw new FormatException("Action message ID must be a valid ID.");
            }

            Enqueued.Add(parsedActionMessageId);
            return "job-id";
        }
    }

    private sealed class FakeCommandEnvelopeRuntime : ICommandEnvelopeRuntime
    {
        public List<CommandEnvelopeRuntimeInvocation> StageInvocations { get; } = [];
        public List<CommandEnvelopeRuntimeInvocation> PersistInvocations { get; } = [];

        public Task<CommandEnvelopeReceipt> PersistAsync(
            CommandEnvelopeRequest request,
            CancellationToken cancellationToken = default)
        {
            PersistInvocations.Add(new CommandEnvelopeRuntimeInvocation(request, cancellationToken));
            throw new AssertionException("Stage-only outbox writing must not persist a command envelope.");
        }

        public Task<CommandEnvelopeReceipt> StageAsync(
            CommandEnvelopeRequest request,
            CancellationToken cancellationToken = default)
        {
            StageInvocations.Add(new CommandEnvelopeRuntimeInvocation(request, cancellationToken));
            return Task.FromResult(new CommandEnvelopeReceipt("stage-envelope-id", StageInvocations.Count > 1));
        }

        public Task<CommandEnvelopeStart> BeginAsync(string envelopeId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CommandEnvelopeFinalization> FinalizeAsync(
            string envelopeId,
            int attemptNumber,
            IReadOnlyList<CommandHandlerResult> results,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordFaultAsync(
            string envelopeId,
            string reason,
            string errorMessage,
            string errorDetails,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordCancellationAsync(string envelopeId) => throw new NotSupportedException();
    }

    private sealed record CommandEnvelopeRuntimeInvocation(
        CommandEnvelopeRequest Request,
        CancellationToken CancellationToken);

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void DetachEntity<TEntity>(TEntity entity)
            where TEntity : class
        {
        }
    }
}
