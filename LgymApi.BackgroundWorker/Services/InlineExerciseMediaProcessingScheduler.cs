using LgymApi.Application.WorkoutProgress.ExerciseMedia;

namespace LgymApi.BackgroundWorker.Services;

// Testing has no Hangfire storage/server; real restart acceptance uses PostgreSQL.
public sealed class InlineExerciseMediaProcessingScheduler(IExerciseMediaProcessor processor) : IExerciseMediaProcessingScheduler
{
    public Task<string> ProcessAsync(string operationKey, byte[] bytes, CancellationToken cancellationToken)
        => processor.ProcessAsync(operationKey, bytes, cancellationToken);
}
