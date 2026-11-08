using System.Collections.Concurrent;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.IntegrationTests;

public sealed class TestPushProviderSender : IPushProviderSender
{
    private readonly ConcurrentQueue<(PushInstallationDeliveryTarget Target, PushEventPayload Payload)> _attempts = new();

    public IReadOnlyCollection<(PushInstallationDeliveryTarget Target, PushEventPayload Payload)> Attempts
        => _attempts.ToArray();

    public Task<PushSendAttemptResult> SendAsync(
        PushInstallationDeliveryTarget target,
        PushEventPayload payload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _attempts.Enqueue((target, payload));

        return Task.FromResult(new PushSendAttemptResult(
            PushSendOutcome.Skipped,
            "TestCapture",
            null,
            null,
            null));
    }
}
