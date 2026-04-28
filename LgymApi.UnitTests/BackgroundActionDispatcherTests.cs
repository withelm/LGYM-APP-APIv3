using FluentAssertions;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class BackgroundActionDispatcherTests
{
    [Test]
    public async Task EnqueueAsync_WithRegisteredHandler_StagesEnvelope_WithoutCommitOrScheduling()
    {
        using var harness = CreateHarness(services =>
            services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler>());

        CommandEnvelope? stagedEnvelope = null;
        harness.Repository
            .AddOrGetExistingAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                stagedEnvelope = callInfo.Arg<CommandEnvelope>();
                return stagedEnvelope;
            });

        await harness.Dispatcher.EnqueueAsync(new TestCommand { Value = "stage-me" });

        await harness.Repository.Received(1)
            .AddOrGetExistingAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>());

        stagedEnvelope.Should().NotBeNull();
        stagedEnvelope!.Status.Should().Be(ActionExecutionStatus.Pending);
        stagedEnvelope.CommandTypeFullName.Should().Be(typeof(TestCommand).FullName);
        stagedEnvelope.PayloadJson.Should().Contain("stage-me");
        harness.UnitOfWork.ReceivedCalls().Should().BeEmpty("dispatcher now only stages work");
        harness.Scheduler.ReceivedCalls().Should().BeEmpty("dispatcher must not schedule work directly");
    }

    [Test]
    public async Task EnqueueAsync_WithReadPhaseDuplicate_ReturnsWithoutCommitOrScheduling()
    {
        using var harness = CreateHarness(services =>
            services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler>());

        CommandEnvelope? attemptedEnvelope = null;
        var existingEnvelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            CommandTypeFullName = typeof(TestCommand).FullName!,
            PayloadJson = "{\"value\":\"duplicate\"}",
            Status = ActionExecutionStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow
        };

        harness.Repository
            .AddOrGetExistingAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                attemptedEnvelope = callInfo.Arg<CommandEnvelope>();
                return existingEnvelope;
            });

        await harness.Dispatcher.EnqueueAsync(new TestCommand { Value = "duplicate" });

        await harness.Repository.Received(1)
            .AddOrGetExistingAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>());

        attemptedEnvelope.Should().NotBeNull();
        attemptedEnvelope.Should().NotBeSameAs(existingEnvelope, "duplicate detection should short-circuit when the repository returns an existing envelope");
        harness.UnitOfWork.ReceivedCalls().Should().BeEmpty("duplicate short-circuit must not commit");
        harness.Scheduler.ReceivedCalls().Should().BeEmpty("duplicate short-circuit must not schedule");
    }

    [Test]
    public async Task EnqueueAsync_WithNoRegisteredHandlers_ShortCircuitsBeforeStaging()
    {
        using var harness = CreateHarness();

        await harness.Dispatcher.EnqueueAsync(new TestCommand { Value = "no-handlers" });

        harness.Repository.ReceivedCalls().Should().BeEmpty("zero-handler dispatch should return before staging");
        harness.UnitOfWork.ReceivedCalls().Should().BeEmpty("zero-handler dispatch must not commit");
        harness.Scheduler.ReceivedCalls().Should().BeEmpty("zero-handler dispatch must not schedule");
    }

    [Test]
    public async Task EnqueueAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        using var harness = CreateHarness(services =>
            services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler>());

        Func<Task> action = () => harness.Dispatcher.EnqueueAsync<TestCommand>(null!);

        await action.Should().ThrowAsync<ArgumentNullException>();
        harness.Repository.ReceivedCalls().Should().BeEmpty();
        harness.UnitOfWork.ReceivedCalls().Should().BeEmpty();
        harness.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    private static DispatcherHarness CreateHarness(Action<IServiceCollection>? configureServices = null)
    {
        var repository = Substitute.For<ICommandEnvelopeRepository>();
        var scheduler = Substitute.For<IActionMessageScheduler>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => new FakeLogger<CommandDispatcher>());
        services.AddSingleton(scheduler);
        services.AddSingleton(unitOfWork);
        configureServices?.Invoke(services);

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new CommandDispatcher(
            serviceProvider,
            repository,
            serviceProvider.GetRequiredService<ILogger<CommandDispatcher>>());

        return new DispatcherHarness(serviceProvider, repository, scheduler, unitOfWork, dispatcher);
    }

    private sealed class DispatcherHarness(
        IDisposable serviceProvider,
        ICommandEnvelopeRepository repository,
        IActionMessageScheduler scheduler,
        IUnitOfWork unitOfWork,
        CommandDispatcher dispatcher) : IDisposable
    {
        public ICommandEnvelopeRepository Repository { get; } = repository;
        public IActionMessageScheduler Scheduler { get; } = scheduler;
        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
        public CommandDispatcher Dispatcher { get; } = dispatcher;

        public void Dispose() => serviceProvider.Dispose();
    }

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class TestCommand : IActionCommand
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class TestActionHandler : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
