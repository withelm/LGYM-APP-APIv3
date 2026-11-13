using System.Threading.Channels;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using Microsoft.Extensions.Hosting;

namespace LgymApi.Infrastructure.Services;

public sealed class ExerciseMediaVerificationQueue(IExerciseMediaVerifier decoder) : BackgroundService, IExerciseMediaVerifier
{
    // One decode at a time, with at most one bounded file waiting in memory.
    private readonly Channel<WorkItem> _queue = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });
    private int _accepting;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        Volatile.Write(ref _accepting, 1);
    }

    public async Task<VerifiedExerciseMedia> VerifyAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length is <= 0 or > ExerciseMediaLimits.MaxBytes) throw new ExerciseMediaRejectedException();
        var item = new WorkItem(bytes, cancellationToken);
        if (Volatile.Read(ref _accepting) == 0 || !_queue.Writer.TryWrite(item))
            throw new InvalidOperationException("Exercise media verification is unavailable.");

        return await item.Completion.Task.WaitAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(item.CancellationToken, stoppingToken);
                try
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var result = await decoder.VerifyAsync(item.Bytes, cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    item.Completion.TrySetResult(result);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    item.Completion.TrySetCanceled(cancellation.Token);
                }
                catch (Exception exception)
                {
                    // A rejected file/provider failure must not terminate the consumer.
                    item.Completion.TrySetException(exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            Volatile.Write(ref _accepting, 0);
            _queue.Writer.TryComplete();
            while (_queue.Reader.TryRead(out var pending))
                pending.Completion.TrySetCanceled(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Volatile.Write(ref _accepting, 0);
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    private sealed record WorkItem(byte[] Bytes, CancellationToken CancellationToken)
    {
        public TaskCompletionSource<VerifiedExerciseMedia> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
