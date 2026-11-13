using FluentAssertions;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Infrastructure.Services;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaVerificationQueueTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static VerifiedExerciseMedia Result(byte[] bytes) => new(bytes, "image/gif", "gif", "hash");

    [Test]
    public async Task Jobs_RunSequentiallyInOrder_AndExcessWorkIsRejected()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<byte>();
        var decoder = Substitute.For<IExerciseMediaVerifier>();
        decoder.VerifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var bytes = call.Arg<byte[]>();
            order.Add(bytes[0]);
            if (bytes[0] == 1) { started.SetResult(); await release.Task; }
            return Result(bytes);
        });
        using var queue = new ExerciseMediaVerificationQueue(decoder);
        await queue.StartAsync(default);
        var first = queue.VerifyAsync([1], default);
        await started.Task.WaitAsync(Timeout);
        var second = queue.VerifyAsync([2], default);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => queue.VerifyAsync([3], default));
            second.IsCompleted.Should().BeFalse();
            order.Should().Equal(1);
        }
        finally { release.TrySetResult(); }
        await Task.WhenAll(first, second).WaitAsync(Timeout);
        order.Should().Equal(1, 2);
        await queue.StopAsync(default);
    }

    [Test]
    public async Task CancelledWaitingJob_IsNeverDecoded()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decoder = Substitute.For<IExerciseMediaVerifier>();
        decoder.VerifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var bytes = call.Arg<byte[]>();
            if (bytes[0] == 1) { started.SetResult(); await release.Task; }
            return Result(bytes);
        });
        using var queue = new ExerciseMediaVerificationQueue(decoder);
        await queue.StartAsync(default);
        var first = queue.VerifyAsync([1], default);
        await started.Task.WaitAsync(Timeout);
        using var cancelled = new CancellationTokenSource();
        var waiting = queue.VerifyAsync([2], cancelled.Token);
        cancelled.Cancel();
        try { await Assert.CatchAsync<OperationCanceledException>(async () => await waiting.WaitAsync(Timeout)); }
        finally { release.TrySetResult(); }
        await first.WaitAsync(Timeout);
        // Shutdown drains the cancelled slot without ever decoding it.
        await queue.StopAsync(default).WaitAsync(Timeout);
        await decoder.DidNotReceive().VerifyAsync(Arg.Is<byte[]>(bytes => bytes[0] == 2), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DecodeFailure_IsReturnedToCaller_AndDoesNotKillWorker()
    {
        var decoder = Substitute.For<IExerciseMediaVerifier>();
        decoder.VerifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(call =>
            call.Arg<byte[]>()[0] == 1
                ? Task.FromException<VerifiedExerciseMedia>(new ExerciseMediaRejectedException())
                : Task.FromResult(Result(call.Arg<byte[]>())));
        using var queue = new ExerciseMediaVerificationQueue(decoder);
        await queue.StartAsync(default);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(() => queue.VerifyAsync([1], default));
        (await queue.VerifyAsync([2], default).WaitAsync(Timeout)).Bytes.Should().Equal(2);
        await queue.StopAsync(default);
    }

    [Test]
    public async Task Shutdown_CancelsActiveAndWaitingJobs_RejectsNewJobs_AndFreshHostWorks()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decoder = Substitute.For<IExerciseMediaVerifier>();
        decoder.VerifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            started.TrySetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, call.Arg<CancellationToken>());
            return Result(call.Arg<byte[]>());
        });
        using var queue = new ExerciseMediaVerificationQueue(decoder);
        await queue.StartAsync(default);
        var active = queue.VerifyAsync([1], default);
        await started.Task.WaitAsync(Timeout);
        var waiting = queue.VerifyAsync([2], default);
        await queue.StopAsync(default).WaitAsync(Timeout);
        await Assert.CatchAsync<OperationCanceledException>(async () => await active.WaitAsync(Timeout));
        await Assert.CatchAsync<OperationCanceledException>(async () => await waiting.WaitAsync(Timeout));
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.VerifyAsync([3], default));

        var newDecoder = Substitute.For<IExerciseMediaVerifier>();
        newDecoder.VerifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult(Result(call.Arg<byte[]>())));
        using var restarted = new ExerciseMediaVerificationQueue(newDecoder);
        await restarted.StartAsync(default);
        (await restarted.VerifyAsync([4], default).WaitAsync(Timeout)).Bytes.Should().Equal(4);
        await restarted.StopAsync(default);
    }

    [Test]
    public async Task WorkBeforeStartup_AndInvalidOrPreCancelledWork_AreRejected()
    {
        var decoder = Substitute.For<IExerciseMediaVerifier>();
        using var queue = new ExerciseMediaVerificationQueue(decoder);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.VerifyAsync([1], default));
        await queue.StartAsync(default);
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(() => queue.VerifyAsync([], default));
        await Assert.ThrowsAsync<ExerciseMediaRejectedException>(() => queue.VerifyAsync(new byte[ExerciseMediaLimits.MaxBytes + 1], default));
        await Assert.CatchAsync<OperationCanceledException>(() => queue.VerifyAsync([1], new CancellationToken(true)));
        await decoder.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default);
        await queue.StopAsync(default);
    }

    [Test]
    public async Task ActiveCallerCancellation_DoesNotStartOverlappingDecode_OrReturnLateSuccess()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decoder = Substitute.For<IExerciseMediaVerifier>();
        decoder.VerifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var bytes = call.Arg<byte[]>();
            if (bytes[0] == 1) { started.SetResult(); await release.Task; }
            return Result(bytes);
        });
        using var queue = new ExerciseMediaVerificationQueue(decoder);
        await queue.StartAsync(default);
        using var cancelled = new CancellationTokenSource();
        var active = queue.VerifyAsync([1], cancelled.Token);
        await started.Task.WaitAsync(Timeout);
        cancelled.Cancel();
        var waiting = queue.VerifyAsync([2], default);
        try
        {
            await Assert.CatchAsync<OperationCanceledException>(async () => await active.WaitAsync(Timeout));
            waiting.IsCompleted.Should().BeFalse();
            await decoder.DidNotReceive().VerifyAsync(Arg.Is<byte[]>(bytes => bytes[0] == 2), Arg.Any<CancellationToken>());
        }
        finally { release.TrySetResult(); }
        (await waiting.WaitAsync(Timeout)).Bytes.Should().Equal(2);
        active.IsCanceled.Should().BeTrue();
        await queue.StopAsync(default);
    }
}
