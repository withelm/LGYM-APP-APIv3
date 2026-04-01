using Hangfire;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Services;

/// <summary>
/// Hangfire-backed scheduler adapter for background action message processing.
/// Enqueues orchestration jobs by durable command envelope id only.
/// </summary>
public sealed class HangfireActionMessageScheduler : IActionMessageScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireActionMessageScheduler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public string? Enqueue(Id<CommandEnvelope> actionMessageId)
    {
        return _backgroundJobClient.Enqueue<IActionMessageJob>(job => job.ExecuteAsync(actionMessageId));
    }
}
