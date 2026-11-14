using System.Threading.RateLimiting;

namespace LgymApi.Api.Features.Exercise.Filters;

internal sealed class ExerciseMediaRequestAdmission : IDisposable
{
    // Keep intake bounded before multipart buffering, independently of decoder work.
    private readonly ConcurrencyLimiter _limiter = new(new ConcurrencyLimiterOptions
    {
        PermitLimit = 2,
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });

    public RateLimitLease TryAcquire() => _limiter.AttemptAcquire();

    public void Dispose() => _limiter.Dispose();
}
