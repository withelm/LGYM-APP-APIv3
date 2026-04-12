using FluentAssertions;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Jobs;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InvitationEmailJobTests
{
    [Test]
    public async Task ExecuteAsync_DelegatesToHandler()
    {
        var notificationId = Id<NotificationMessage>.New();
        var handler = new FakeEmailJobHandler();
        var job = new InvitationEmailJob(handler);

        await job.ExecuteAsync(notificationId);

        handler.Calls.Should().Be(1);
        handler.LastNotificationId.Should().Be(notificationId);
    }

    private sealed class FakeEmailJobHandler : IEmailJobHandler
    {
        public int Calls { get; private set; }
        public Id<NotificationMessage> LastNotificationId { get; private set; }

        public Task ProcessAsync(Id<NotificationMessage> notificationId, CancellationToken cancellationToken = default)
        {
            Calls += 1;
            LastNotificationId = notificationId;
            return Task.CompletedTask;
        }
    }
}
