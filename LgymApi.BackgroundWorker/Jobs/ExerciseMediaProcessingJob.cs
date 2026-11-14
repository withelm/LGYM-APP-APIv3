using Hangfire;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;

namespace LgymApi.BackgroundWorker.Jobs;

[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
public sealed class ExerciseMediaProcessingJob(IExerciseMediaProcessor processor)
{
    // Hangfire omits Result for null returns; use an explicit durable rejection result.
    public const string RejectedResult = "exercise-media:rejected";
    // PostgreSQL-backed Hangfire locking serializes work across processes.
    [DisableConcurrentExecution(60)]
    public async Task<string> ExecuteAsync(string operationKey, byte[] bytes, CancellationToken cancellationToken = default)
    {
        try
        {
            return await processor.ProcessAsync(operationKey, bytes, cancellationToken);
        }
        catch (ExerciseMediaRejectedException) { return RejectedResult; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Hangfire persists failures; provider messages can contain credentials or signed URLs.
            throw new InvalidOperationException("Exercise media processing failed.");
        }
    }
}
