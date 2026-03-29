using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Services;

/// <summary>
/// No-op scheduler for test mode. Does not enqueue any jobs.
/// </summary>
public sealed class NoOpActionMessageScheduler : IActionMessageScheduler
{
    public string? Enqueue(Id<CommandEnvelope> actionMessageId)
    {
        return $"noop-command-{actionMessageId}";
    }
}
