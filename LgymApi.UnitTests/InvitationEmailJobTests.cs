using LgymApi.Application.Notifications;
using LgymApi.Infrastructure.Jobs;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InvitationEmailJobTests
{
    [Test]
    public async Task ExecuteAsync_DelegatesToHandler()
    {
        var notificationId = Guid.NewGuid();
        var handler = new FakeInvitationEmailJobHandler();
        var job = new InvitationEmailJob(handler);

        await job.ExecuteAsync(notificationId);

        Assert.That(handler.Calls, Is.EqualTo(1));
        Assert.That(handler.LastNotificationId, Is.EqualTo(notificationId));
    }

    private sealed class FakeInvitationEmailJobHandler : IInvitationEmailJobHandler
    {
        public int Calls { get; private set; }
        public Guid LastNotificationId { get; private set; }

        public Task ProcessAsync(Guid notificationId, CancellationToken cancellationToken = default)
        {
            Calls += 1;
            LastNotificationId = notificationId;
            return Task.CompletedTask;
        }
    }
}
