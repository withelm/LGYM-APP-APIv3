using LgymApi.Application.Notifications;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LgymApi.IntegrationTests;

public sealed partial class RecurringReportReminderIntegrationTests
{
    [TestCase(true, true, true)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, true)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    [TestCase(false, false, false)]
    public async Task ReminderDelivery_InFlightSend_BlocksUnlinkOrSourceInvalidation(
        bool upcoming, bool remotePush, bool unlink)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        var scenario = await SeedDeliveryRaceAsync(upcoming, remotePush);
        var gate = new DeliveryGate { PauseSend = true };
        using var factory = CreateDeliveryRaceFactory(gate, remotePush);
        await using var deliveryScope = factory.Services.CreateAsyncScope();
        var delivery = StartDeliveryAsync(deliveryScope.ServiceProvider, scenario, remotePush, ct);
        try
        {
            await gate.SendReached.Task.WaitAsync(ct);
            // PostgreSQL, rather than a client-side timing guess, proves the competing
            // writer cannot acquire its row lock while the send is in progress.
            await using var competingScope = Factory.Services.CreateAsyncScope();
            var database = competingScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(ct);
            await database.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '500ms'", ct);
            Func<Task> invalidate = () => InvalidateReminderAsync(competingScope.ServiceProvider, scenario, upcoming, unlink, ct);
            var failure = (await Assert.CatchAsync<InvalidOperationException>(async () => { await invalidate(); }))!;
            Assert.That(failure.InnerException, Is.TypeOf<DbUpdateException>());
            var assertedValue62 = ((DbUpdateException)failure.InnerException!).InnerException;
            Assert.That(assertedValue62, Is.TypeOf<PostgresException>());
            Assert.That(((PostgresException)assertedValue62).SqlState, Is.EqualTo(PostgresErrorCodes.LockNotAvailable));
            await transaction.RollbackAsync(ct);
        }
        finally
        {
            gate.ReleaseSend.TrySetResult();
        }

        await delivery.WaitAsync(ct);
        await using var unlinkScope = Factory.Services.CreateAsyncScope();
        await InvalidateReminderAsync(unlinkScope.ServiceProvider, scenario, upcoming, unlink, ct);
        Assert.That(gate.SendCount, Is.EqualTo(1));
        await AssertDeliveryMessageAsync(scenario, remotePush, true, ct);
    }

    [TestCase(true, true, true)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, true)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    [TestCase(false, false, false)]
    public async Task ReminderDelivery_InvalidationBeforeDeliveryFence_SuppressesExternalSend(
        bool upcoming, bool remotePush, bool unlink)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        var scenario = await SeedDeliveryRaceAsync(upcoming, remotePush);
        var gate = new DeliveryGate { PauseFence = true };
        using var factory = CreateDeliveryRaceFactory(gate, remotePush);
        await using var deliveryScope = factory.Services.CreateAsyncScope();
        var delivery = StartDeliveryAsync(deliveryScope.ServiceProvider, scenario, remotePush, ct);
        try
        {
            await gate.FenceReached.Task.WaitAsync(ct);
            await using var invalidationScope = Factory.Services.CreateAsyncScope();
            await InvalidateReminderAsync(invalidationScope.ServiceProvider, scenario, upcoming, unlink, ct);
        }
        finally
        {
            gate.ReleaseFence.TrySetResult();
        }

        await delivery.WaitAsync(ct);
        Assert.That(gate.SendCount, Is.EqualTo(0));
        await AssertDeliveryMessageAsync(scenario, remotePush, false, ct);
    }

    private async Task AssertDeliveryMessageAsync(DeliveryScenario scenario, bool remotePush, bool sent, CancellationToken ct)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (remotePush)
        {
            var message = await database.PushNotificationMessages.AsNoTracking().SingleAsync(item => item.Id == scenario.MessageId, ct);
            Assert.That(message.Status, Is.EqualTo(sent ? Domain.Enums.PushNotificationStatus.Sent : Domain.Enums.PushNotificationStatus.Skipped));
            Assert.That(message.Attempts, Is.EqualTo(sent ? 1 : 0));
            if (!sent) Assert.That(message.ProviderStatus, Is.EqualTo("SourceNotificationUnavailable"));
        }
        else
        {
            Assert.That((await database.InAppNotifications.CountAsync(item => item.DeliveryKey == scenario.Input.DeliveryKey, ct)), Is.EqualTo(1));
        }
    }

    private static async Task StartDeliveryAsync(IServiceProvider services, DeliveryScenario scenario, bool remotePush, CancellationToken ct)
    {
        if (remotePush)
        {
            await services.GetRequiredService<IPushNotificationDeliveryService>().ProcessAsync(scenario.MessageId.ToString(), ct);
        }
        else
        {
            var result = await services.GetRequiredService<IInAppNotificationService>().CreateAsync(scenario.Input, ct);
            Assert.That(result.IsSuccess, Is.True);
        }
    }
}
