using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.WorkoutProgress.ExerciseMedia;

internal sealed class ExerciseMediaProcessor(IExerciseMediaVerifier verifier, IExerciseMediaStorage storage) : IExerciseMediaProcessor
{
    public async Task<string> ProcessAsync(string operationKey, byte[] bytes, CancellationToken cancellationToken)
    {
        if (!Id<CorrelationScope>.TryParse(operationKey, out var operationId) || operationId.IsEmpty)
            throw new ExerciseMediaRejectedException();
        if (!storage.IsConfigured) throw new InvalidOperationException("Exercise media storage is unavailable.");
        var verified = await verifier.VerifyAsync(bytes, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var filename = verified.Extension == "gif" ? "animation.gif" : "image." + verified.Extension;
        var key = $"exercises/{operationId}/{verified.PublishedSha256}/{filename}";
        return await storage.PublishAsync(key, verified.Bytes, verified.ContentType, cancellationToken);
    }
}
