using LgymApi.Application.Features.PasswordReset.Contracts;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Application.Notifications.Contracts.Email;
using LgymApi.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PasswordRecoveryEmailSchedulerAdapterTests
{
    [Test]
    public async Task ScheduleAsync_MapsSevenFieldIdentityRequestToWorkerPayload()
    {
        var scheduler = new CapturingEmailScheduler();
        var adapter = new PasswordRecoveryEmailSchedulerAdapter(scheduler);
        var cancellationToken = new CancellationTokenSource().Token;
        var request = new PasswordRecoveryEmailRequest(
            ParseId<User>("12121212-1212-1212-1212-121212121212"),
            ParseId<PasswordResetToken>("34343434-3434-3434-3434-343434343434"),
            "Identity Adapter User",
            "identity.adapter@example.test",
            "identity-reset-token-380",
            "https://identity-sentinel.example.test/reset",
            "!invalid-culture-380");

        await adapter.ScheduleAsync(request, cancellationToken);

        Assert.That(typeof(PasswordRecoveryEmailRequest).IsPublic, Is.True);
        Assert.That(typeof(PasswordRecoveryEmailSchedulerAdapter).IsNotPublic, Is.True);
        Assert.That(typeof(PasswordRecoveryEmailRequest).GetProperties()
            .Select(property => property.Name), Is.EqualTo(new[] { "UserId", "TokenId", "UserName", "RecipientEmail", "ResetToken", "ResetUrl", "CultureName" }));
        var assertedValue137 = (typeof(PasswordRecoveryEmailSchedulerAdapter).GetConstructors()).ToArray();
        Assert.That(assertedValue137, Has.Exactly(1).Items);
        var adapterConstructor = assertedValue137.Single();
        Assert.That(adapterConstructor.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(IEmailSchedulingPort<PasswordRecoveryEmailPayload>) }));
        var scheduleMethod = typeof(PasswordRecoveryEmailSchedulerAdapter)
            .GetMethod(nameof(PasswordRecoveryEmailSchedulerAdapter.ScheduleAsync));
        Assert.That(scheduleMethod, Is.Not.Null);
        Assert.That(scheduleMethod!.ReturnType, Is.EqualTo(typeof(Task)));
        Assert.That(scheduleMethod.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(PasswordRecoveryEmailRequest), typeof(CancellationToken) }));
        Assert.That(scheduler.Payload, Is.Not.Null);
        Assert.That(scheduler.Payload!.UserId, Is.EqualTo(request.UserId));
        Assert.That(scheduler.Payload.TokenId, Is.EqualTo(request.TokenId));
        Assert.That(scheduler.Payload.UserName, Is.EqualTo(request.UserName));
        Assert.That(scheduler.Payload.RecipientEmail, Is.EqualTo(request.RecipientEmail));
        Assert.That(scheduler.Payload.ResetToken, Is.EqualTo(request.ResetToken));
        Assert.That(scheduler.Payload.ResetUrl, Is.EqualTo("https://identity-sentinel.example.test/reset"));
        Assert.That(scheduler.Payload.CultureName, Is.EqualTo(request.CultureName));
        Assert.That(scheduler.Payload.CorrelationId.GetValue(), Is.EqualTo(request.TokenId.GetValue()));
        Assert.That(scheduler.Payload.NotificationType.Value, Is.EqualTo("user.password.recovery"));
        Assert.That(scheduler.Payload.Culture.Name, Is.EqualTo("en-US"));
        Assert.That(scheduler.CancellationToken, Is.EqualTo(cancellationToken));
    }

    [Test]
    public async Task ScheduleAsync_ForwardsCancellationToWorkerScheduler()
    {
        var scheduler = new CapturingEmailScheduler();
        var adapter = new PasswordRecoveryEmailSchedulerAdapter(scheduler);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var request = new PasswordRecoveryEmailRequest(
            ParseId<User>("56565656-5656-5656-5656-565656565656"),
            ParseId<PasswordResetToken>("78787878-7878-7878-7878-787878787878"),
            "Cancelled Identity User",
            "cancelled.adapter@example.test",
            "cancelled-token-380",
            "https://cancelled-sentinel.example.test/reset",
            "en-US");

        var action = () => adapter.ScheduleAsync(request, cancellationSource.Token);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<OperationCanceledException>());
        Assert.That(scheduler.CancellationToken, Is.EqualTo(cancellationSource.Token));
    }

    private static Id<TEntity> ParseId<TEntity>(string value)
    {
        Assert.That(Id<TEntity>.TryParse(value, out var id), Is.True);
        return id;
    }

    private sealed class CapturingEmailScheduler : IEmailSchedulingPort<PasswordRecoveryEmailPayload>
    {
        public PasswordRecoveryEmailPayload? Payload { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task ScheduleAsync(PasswordRecoveryEmailPayload payload, CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            Payload = payload;
            return Task.CompletedTask;
        }
    }
}
