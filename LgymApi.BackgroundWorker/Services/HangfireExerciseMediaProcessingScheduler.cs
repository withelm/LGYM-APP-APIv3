using System.Text.Json;
using Hangfire;
using Hangfire.States;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.BackgroundWorker.Jobs;
using LgymApi.Application.Platform.Contracts.Serialization;

namespace LgymApi.BackgroundWorker.Services;

public sealed class HangfireExerciseMediaProcessingScheduler(IBackgroundJobClient client, JobStorage storage) : IExerciseMediaProcessingScheduler
{
    public async Task<string> ProcessAsync(string operationKey, byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(operationKey) || bytes.Length is <= 0 or > ExerciseMediaLimits.MaxBytes)
            throw new ExerciseMediaRejectedException();
        // Cancelling the caller's wait never deletes the persisted job or cancels its worker.
        var jobId = client.Enqueue<ExerciseMediaProcessingJob>(job => job.ExecuteAsync(operationKey, bytes, CancellationToken.None));
        if (string.IsNullOrEmpty(jobId)) throw new InvalidOperationException("Exercise media scheduling failed.");
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var connection = storage.GetConnection())
            {
                var state = connection.GetStateData(jobId);
                if (state?.Name == SucceededState.StateName)
                {
                    if (!state.Data.TryGetValue("Result", out var result))
                        throw new InvalidOperationException("Exercise media result is unavailable.");
                    var image = JsonSerializer.Deserialize<string?>(result, SharedSerializationOptions.Current);
                    if (image == ExerciseMediaProcessingJob.RejectedResult) throw new ExerciseMediaRejectedException();
                    if (string.IsNullOrWhiteSpace(image)) throw new InvalidOperationException("Exercise media result is unavailable.");
                    return image;
                }
                if (state is null || state.Name is "Failed" or "Deleted")
                    throw new InvalidOperationException("Exercise media processing is unavailable.");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
    }
}
