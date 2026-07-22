using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
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
        var writerType = GetRequiredApplicationType(
            CommandOutboxWriterTypeName,
            "T5 must add the Application-owned ICommandOutboxWriter port.");
        var resultType = GetRequiredApplicationType(
            CommandEnvelopeStageResultTypeName,
            "T5 must add CommandEnvelopeStageResult so callers can distinguish newly staged and existing envelopes.");

        writerType.IsInterface.Should().BeTrue();
        var method = writerType.GetMethod("StageAsync", BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull("ICommandOutboxWriter must expose StageAsync<TCommand>.");
        method!.IsGenericMethodDefinition.Should().BeTrue();
        method.GetParameters().Select(parameter => parameter.Name).Should().Equal("command", "cancellationToken");
        method.GetParameters()[1].IsOptional.Should().BeTrue();

        var genericParameter = method.GetGenericArguments().Should().ContainSingle().Subject;
        genericParameter.GenericParameterAttributes.Should().Be(GenericParameterAttributes.ReferenceTypeConstraint);
        genericParameter.GetGenericParameterConstraints().Should().Equal(actionCommandType);
        method.ReturnType.Should().Be(typeof(Task<>).MakeGenericType(resultType));
        resultType.GetProperty("Envelope", BindingFlags.Public | BindingFlags.Instance).Should().NotBeNull();
        resultType.GetProperty("WasExisting", BindingFlags.Public | BindingFlags.Instance).Should().NotBeNull();
    }

    [Test]
    public async Task StageAsync_StagesEnvelopeWithSharedSerializationAndNeverSavesOrSchedules()
    {
        var repository = new FakeCommandEnvelopeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var scheduler = new FakeActionMessageScheduler();
        var command = new TestStageCommand { Value = "stage-only" };
        var writer = CreateWriter(
            repository,
            unitOfWork,
            scheduler,
            includeHandler: true);

        var firstResult = await InvokeStageAsync(writer, command);
        var firstEnvelope = GetStageEnvelope(firstResult);
        var duplicateResult = await InvokeStageAsync(writer, command);

        repository.Envelopes.Should().ContainSingle();
        firstEnvelope.Status.Should().Be(ActionExecutionStatus.Pending);
        firstEnvelope.CommandTypeFullName.Should().Be("Tests.AcceptedProgress.StageCommand");
        firstEnvelope.PayloadJson.Should().Be(JsonSerializer.Serialize(command, SharedSerializationOptions.Current));
        unitOfWork.SaveCallCount.Should().Be(0, "StageAsync must leave commit timing to its caller");
        scheduler.Enqueued.Should().BeEmpty("committed-intent dispatch must not run before the caller commits its UoW");
        GetStageEnvelope(duplicateResult).Should().BeSameAs(firstEnvelope);
        GetWasExisting(firstResult).Should().BeFalse();
        GetWasExisting(duplicateResult).Should().BeTrue();
    }

    [Test]
    public async Task StageAsync_WhenNoExactHandlerExists_DoesNotStageSaveOrSchedule()
    {
        var repository = new FakeCommandEnvelopeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var scheduler = new FakeActionMessageScheduler();
        var writer = CreateWriter(
            repository,
            unitOfWork,
            scheduler,
            includeHandler: false);

        await InvokeStageAsync(writer, new TestStageCommand { Value = "no-handler" });

        repository.Envelopes.Should().BeEmpty("StageAsync must validate exact handler availability before staging");
        unitOfWork.SaveCallCount.Should().Be(0);
        scheduler.Enqueued.Should().BeEmpty();
    }

    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.Applied)]
    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.Duplicate)]
    public async Task AcceptedProgressCommandHandler_AppliedAndDuplicate_Complete(
        ReportSubmissionAcceptedProgressConsumeOutcome outcome)
    {
        var consumer = Substitute.For<IReportSubmissionAcceptedProgressConsumer>();
        consumer.ConsumeAsync(Arg.Any<ReportSubmissionAcceptedProgressEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateConsumeResult(outcome, "unused")));
        var handler = CreateAcceptedProgressHandler(consumer);

        await ExecuteAcceptedProgressHandlerAsync(handler, CreateValidEvent());

        await consumer.Received(1).ConsumeAsync(Arg.Any<ReportSubmissionAcceptedProgressEvent>(), Arg.Any<CancellationToken>());
    }

    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.Invalid)]
    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.UnsupportedSchema)]
    [TestCase(ReportSubmissionAcceptedProgressConsumeOutcome.Poison)]
    public async Task AcceptedProgressCommandHandler_InvalidUnsupportedAndPoison_ThrowSanitizedBoundedFailure(
        ReportSubmissionAcceptedProgressConsumeOutcome outcome)
    {
        var privateDetail = $"payload-json:{new string('x', 512)}";
        var consumer = Substitute.For<IReportSubmissionAcceptedProgressConsumer>();
        consumer.ConsumeAsync(Arg.Any<ReportSubmissionAcceptedProgressEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateConsumeResult(outcome, privateDetail)));
        var handler = CreateAcceptedProgressHandler(consumer);

        var action = () => ExecuteAcceptedProgressHandlerAsync(handler, CreateValidEvent());

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain(outcome.ToString());
        exception.Which.Message.Should().NotContain(privateDetail);
        exception.Which.Message.Length.Should().BeLessThanOrEqualTo(256);
    }

    [Test]
    public async Task AcceptedProgressCommandHandler_UnexpectedConsumerException_RemainsRetryable()
    {
        var consumer = Substitute.For<IReportSubmissionAcceptedProgressConsumer>();
        consumer.ConsumeAsync(Arg.Any<ReportSubmissionAcceptedProgressEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReportSubmissionAcceptedProgressConsumeResult>(
                new TimeoutException("temporary consumer outage")));
        var handler = CreateAcceptedProgressHandler(consumer);

        var action = () => ExecuteAcceptedProgressHandlerAsync(handler, CreateValidEvent());

        await action.Should().ThrowAsync<TimeoutException>().WithMessage("temporary consumer outage");
    }

    private object CreateWriter(
        FakeCommandEnvelopeRepository repository,
        FakeUnitOfWork unitOfWork,
        FakeActionMessageScheduler scheduler,
        bool includeHandler)
    {
        var writerPort = GetRequiredApplicationType(
            CommandOutboxWriterTypeName,
            "T5 must add ICommandOutboxWriter before staging can be tested.");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICommandEnvelopeRepository>(repository);
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
        var implementationType = typeof(CommandDispatcher).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && writerPort.IsAssignableFrom(type))
            .Should().ContainSingle("T5 must provide exactly one runtime implementation of ICommandOutboxWriter.")
            .Subject;

        return ActivatorUtilities.CreateInstance(_serviceProvider, implementationType);
    }

    private object CreateAcceptedProgressHandler(IReportSubmissionAcceptedProgressConsumer consumer)
    {
        var handlerType = GetRequiredWorkerType(
            AcceptedProgressHandlerTypeName,
            "T9 must add ReportSubmissionAcceptedProgressCommandHandler.");
        var commandType = GetRequiredApplicationType(
            AcceptedProgressCommandTypeName,
            "T6 must add the Reporting-owned ReportSubmissionAcceptedProgressCommand.");
        var actionInterface = typeof(IBackgroundAction<>).MakeGenericType(commandType);
        handlerType.Should().BeAssignableTo(actionInterface);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(consumer);
        _serviceProvider = services.BuildServiceProvider();
        return ActivatorUtilities.CreateInstance(_serviceProvider, handlerType);
    }

    private static async Task<object> InvokeStageAsync(object writer, TestStageCommand command)
    {
        var writerType = GetRequiredApplicationType(
            CommandOutboxWriterTypeName,
            "T5 must add ICommandOutboxWriter before StageAsync can be invoked.");
        var method = writerType.GetMethod("StageAsync", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new AssertionException("ICommandOutboxWriter must expose StageAsync<TCommand>.");
        var task = method.MakeGenericMethod(typeof(TestStageCommand))
            .Invoke(writer, [command, CancellationToken.None]) as Task;

        task.Should().NotBeNull("StageAsync must return a Task<CommandEnvelopeStageResult>.");
        await task!;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static async Task ExecuteAcceptedProgressHandlerAsync(
        object handler,
        ReportSubmissionAcceptedProgressEvent @event)
    {
        var commandType = GetRequiredApplicationType(
            AcceptedProgressCommandTypeName,
            "T6 must add ReportSubmissionAcceptedProgressCommand before the Worker handler can execute it.");
        var command = CreateAcceptedProgressCommand(commandType, @event);
        var actionInterface = typeof(IBackgroundAction<>).MakeGenericType(commandType);
        var executeMethod = actionInterface.GetMethod("ExecuteAsync")
            ?? throw new AssertionException("The accepted-progress handler must implement IBackgroundAction<TCommand>.");
        var task = executeMethod.Invoke(handler, [command, CancellationToken.None]) as Task;

        task.Should().NotBeNull();
        await task!;
    }

    private static object CreateAcceptedProgressCommand(Type commandType, ReportSubmissionAcceptedProgressEvent @event)
    {
        var eventConstructor = commandType.GetConstructors()
            .SingleOrDefault(constructor => constructor.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(ReportSubmissionAcceptedProgressEvent));
        if (eventConstructor != null)
        {
            return eventConstructor.Invoke([@event]);
        }

        var command = Activator.CreateInstance(commandType)
            ?? throw new AssertionException("ReportSubmissionAcceptedProgressCommand must be instantiable.");
        var eventProperty = commandType.GetProperty("Event", BindingFlags.Public | BindingFlags.Instance);
        eventProperty.Should().NotBeNull(
            "ReportSubmissionAcceptedProgressCommand must carry its nested accepted-progress Event.");
        eventProperty!.SetValue(command, @event);
        return command;
    }

    private static CommandEnvelope GetStageEnvelope(object result)
    {
        var envelope = result.GetType().GetProperty("Envelope", BindingFlags.Public | BindingFlags.Instance)?.GetValue(result);
        envelope.Should().BeOfType<CommandEnvelope>();
        return (CommandEnvelope)envelope!;
    }

    private static bool GetWasExisting(object result)
    {
        var wasExisting = result.GetType().GetProperty("WasExisting", BindingFlags.Public | BindingFlags.Instance)?.GetValue(result);
        wasExisting.Should().BeOfType<bool>();
        return (bool)wasExisting!;
    }

    private static Type GetRequiredApplicationType(string typeName, string because)
    {
        var type = typeof(LgymApi.Application.ServiceCollectionExtensions).Assembly.GetType(typeName);
        type.Should().NotBeNull(because);
        return type!;
    }

    private static Type GetRequiredWorkerType(string typeName, string because)
    {
        var type = typeof(CommandDispatcher).Assembly.GetType(typeName);
        type.Should().NotBeNull(because);
        return type!;
    }

    private static ReportSubmissionAcceptedProgressConsumeResult CreateConsumeResult(
        ReportSubmissionAcceptedProgressConsumeOutcome outcome,
        string detail)
    {
        return outcome switch
        {
            ReportSubmissionAcceptedProgressConsumeOutcome.Applied => ReportSubmissionAcceptedProgressConsumeResult.Applied(),
            ReportSubmissionAcceptedProgressConsumeOutcome.Duplicate => ReportSubmissionAcceptedProgressConsumeResult.Duplicate(),
            ReportSubmissionAcceptedProgressConsumeOutcome.Invalid => ReportSubmissionAcceptedProgressConsumeResult.Invalid(detail),
            ReportSubmissionAcceptedProgressConsumeOutcome.UnsupportedSchema => ReportSubmissionAcceptedProgressConsumeResult.UnsupportedSchema(detail),
            ReportSubmissionAcceptedProgressConsumeOutcome.Poison => ReportSubmissionAcceptedProgressConsumeResult.Poison(detail),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
    }

    private static ReportSubmissionAcceptedProgressEvent CreateValidEvent()
    {
        return new ReportSubmissionAcceptedProgressEvent(
            1,
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
            "00000000-0000-0000-0000-000000000003",
            "00000000-0000-0000-0000-000000000004",
            ParseId<User>("00000000-0000-0000-0000-000000000005"),
            new DateTimeOffset(2026, 7, 20, 8, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 20, 8, 31, 0, TimeSpan.Zero),
            [new ReportSubmissionAcceptedMeasurement(BodyParts.Chest, 101.5, MeasurementUnits.Centimeters)]);
    }

    private static Id<TEntity> ParseId<TEntity>(string value)
        where TEntity : class
    {
        Id<TEntity>.TryParse(value, out var id).Should().BeTrue();
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

        public string? Enqueue(Id<CommandEnvelope> actionMessageId)
        {
            Enqueued.Add(actionMessageId);
            return "job-id";
        }
    }

    private sealed class FakeCommandEnvelopeRepository : ICommandEnvelopeRepository
    {
        public List<CommandEnvelope> Envelopes { get; } = [];

        public Task AddAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Envelopes.Add(envelope);
            return Task.CompletedTask;
        }

        public Task<CommandEnvelope?> FindByIdAsync(Id<CommandEnvelope> id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelopes.FirstOrDefault(envelope => envelope.Id == id));

        public Task<CommandEnvelope?> FindByCorrelationIdAsync(
            Id<CorrelationScope> correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelopes.FirstOrDefault(envelope => envelope.CorrelationId == correlationId));

        public Task<List<CommandEnvelope>> GetPendingRetriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelopes.Where(envelope => envelope.Status == ActionExecutionStatus.Failed).ToList());

        public Task UpdateAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Detach(CommandEnvelope envelope)
        {
        }

        public Task<CommandEnvelope> AddOrGetExistingAsync(
            CommandEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            var existing = Envelopes.FirstOrDefault(candidate => candidate.CorrelationId == envelope.CorrelationId);
            if (existing != null)
            {
                return Task.FromResult(existing);
            }

            Envelopes.Add(envelope);
            return Task.FromResult(envelope);
        }

        public Task<List<CommandEnvelope>> GetPendingUndispatchedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelopes.Where(envelope => envelope.Status == ActionExecutionStatus.Pending && envelope.DispatchedAt == null).ToList());

        public Task<List<CommandEnvelope>> GetFailedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelopes.Where(envelope => envelope.Status == ActionExecutionStatus.Failed).ToList());

        public Task<List<CommandEnvelope>> GetDeadLetteredAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelopes.Where(envelope => envelope.Status == ActionExecutionStatus.DeadLettered).ToList());

        public Task<int> CountByStatusAsync(ActionExecutionStatus status, CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelopes.Count(envelope => envelope.Status == status));

        public Task<int> DeleteCompletedOlderThanAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

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
