using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.BackgroundWorker.Jobs;
using LgymApi.BackgroundWorker.Services;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaDurableJobTests
{
    private const string Operation = "00000000-0000-0000-0000-000000000123";
    private const string ImageUrl = "https://media.test/exercises/result.gif";

    private static (HangfireExerciseMediaProcessingScheduler Scheduler, IBackgroundJobClient Client, IStorageConnection Connection) Scheduler(string state, string? result = null)
    {
        var client = Substitute.For<IBackgroundJobClient>();
        client.Create(Arg.Any<Job>(), Arg.Any<IState>()).Returns("42");
        var storage = Substitute.For<JobStorage>();
        var connection = Substitute.For<IStorageConnection>();
        storage.GetConnection().Returns(connection);
        connection.GetStateData("42").Returns(new StateData
        {
            Name = state,
            Data = result is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["Result"] = result }
        });
        return (new(client, storage), client, connection);
    }

    [Test]
    public async Task Scheduler_PersistsConcreteFilteredJob_WithBytesStableOperationAndWorkerCancellation()
    {
        var (scheduler, client, _) = Scheduler("Succeeded", JsonSerializer.Serialize(ImageUrl));
        using var caller = new CancellationTokenSource();
        var bytes = new byte[] { 1, 2, 3 };
        Assert.That((await scheduler.ProcessAsync(Operation, bytes, caller.Token)), Is.EqualTo(ImageUrl));
        client.Received(1).Create(Arg.Is<Job>(job => job.Type == typeof(ExerciseMediaProcessingJob)
            && job.Method.Name == "ExecuteAsync" && (string)job.Args[0] == Operation
            && ReferenceEquals(job.Args[1], bytes) && (CancellationToken)job.Args[2] == CancellationToken.None), Arg.Is<EnqueuedState>(state => state.Queue == "default"));
        var filters = JobFilterProviders.Providers.GetFilters(new Job(typeof(ExerciseMediaProcessingJob), typeof(ExerciseMediaProcessingJob).GetMethod("ExecuteAsync")!, [Operation, bytes, CancellationToken.None])).ToArray();
        Assert.That((filters).Any(filter => filter.Instance is DisableConcurrentExecutionAttribute), Is.True);
        Assert.That((filters).Any(filter => filter.Instance is AutomaticRetryAttribute), Is.True);
    }

    [Test]
    public async Task CallerCancellation_StopsWaiting_WithoutDeletingOrCancellingPersistedWork()
    {
        var (scheduler, client, _) = Scheduler("Enqueued");
        using var caller = new CancellationTokenSource();
        var waiting = scheduler.ProcessAsync(Operation, [1], caller.Token);
        caller.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await waiting);
        Assert.That((client.ReceivedCalls()).Where(call => call.GetMethodInfo().Name == nameof(IBackgroundJobClient.Create)), Has.Exactly(1).Items);
        Assert.That((client.ReceivedCalls()).Any(call => call.GetMethodInfo().Name == nameof(IBackgroundJobClient.ChangeState)), Is.False);
    }

    [Test]
    public async Task CancelledOrInvalidInput_DoesNotEnqueue()
    {
        var (scheduler, client, _) = Scheduler("Enqueued");
        await Assert.CatchAsync<OperationCanceledException>(() => scheduler.ProcessAsync(Operation, [1], new CancellationToken(true)));
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(() => scheduler.ProcessAsync("", [1], default));
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(() => scheduler.ProcessAsync(Operation, [], default));
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(() => scheduler.ProcessAsync(Operation, new byte[ExerciseMediaLimits.MaxBytes + 1], default));
        Assert.That(client.ReceivedCalls(), Is.Empty);
    }

    [TestCase("Failed", null)]
    [TestCase("Deleted", null)]
    [TestCase("Succeeded", null)]
    [TestCase("Succeeded", "\"\"")]
    public async Task FailureOrMissingResult_DoesNotAcknowledgeAnUpload(string state, string? result)
    {
        var (scheduler, _, _) = Scheduler(state, result);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scheduler.ProcessAsync(Operation, [1], default));
    }

    [Test]
    public async Task RejectedResult_IsMappedToExistingInvalidMediaContract()
    {
        var persisted = new SucceededState(ExerciseMediaProcessingJob.RejectedResult, 0, 0).SerializeData();
        var (scheduler, _, _) = Scheduler("Succeeded", persisted["Result"]);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(() => scheduler.ProcessAsync(Operation, [1], default));
    }

    [Test]
    public async Task Job_UsesSavedOperationAndWorkerToken_AndDoesNotRetryRejectedBytes()
    {
        var processor = Substitute.For<IExerciseMediaProcessor>();
        using var worker = new CancellationTokenSource();
        processor.ProcessAsync(Operation, Arg.Any<byte[]>(), worker.Token).Returns(ImageUrl);
        var job = new ExerciseMediaProcessingJob(processor);
        Assert.That((await job.ExecuteAsync(Operation, [1], worker.Token)), Is.EqualTo(ImageUrl));
        processor.ProcessAsync(Operation, Arg.Any<byte[]>(), worker.Token).Returns(Task.FromException<string>(new ExerciseMediaRejectedException()));
        Assert.That((await job.ExecuteAsync(Operation, [1], worker.Token)), Is.EqualTo(ExerciseMediaProcessingJob.RejectedResult));
        processor.ProcessAsync("not-an-operation", Arg.Any<byte[]>(), worker.Token).Returns(Task.FromException<string>(new ExerciseMediaRejectedException()));
        Assert.That((await job.ExecuteAsync("not-an-operation", [1], worker.Token)), Is.EqualTo(ExerciseMediaProcessingJob.RejectedResult));
    }

    [Test]
    public async Task Job_PreservesShutdownCancellation_AndSanitizesPersistedProviderFailures()
    {
        var processor = Substitute.For<IExerciseMediaProcessor>();
        using var worker = new CancellationTokenSource();
        worker.Cancel();
        processor.ProcessAsync(Operation, Arg.Any<byte[]>(), worker.Token).Returns(Task.FromCanceled<string>(worker.Token));
        var job = new ExerciseMediaProcessingJob(processor);
        await Assert.CatchAsync<OperationCanceledException>(() => job.ExecuteAsync(Operation, [1], worker.Token));
        processor.ProcessAsync(Operation, Arg.Any<byte[]>(), default).Returns(Task.FromException<string>(new IOException("synthetic-private-provider-secret")));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(Operation, [1]));
        Assert.That(error!.ToString(), Does.Not.Contain("synthetic-private-provider-secret"));
        Assert.That(error.InnerException, Is.Null);
    }
}
