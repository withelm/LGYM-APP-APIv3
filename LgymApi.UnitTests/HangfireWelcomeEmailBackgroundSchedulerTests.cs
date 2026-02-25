using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LgymApi.Application.Notifications;
using LgymApi.Infrastructure.Jobs;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class HangfireEmailBackgroundSchedulerTests
{
    [Test]
    public void Enqueue_CreatesHangfireJob_ForWelcomeEmailJob()
    {
        var client = new FakeBackgroundJobClient();
        var scheduler = new HangfireEmailBackgroundScheduler(client);
        var notificationId = Guid.NewGuid();

        scheduler.Enqueue(notificationId);

        Assert.Multiple(() =>
        {
            Assert.That(client.CreatedJobs, Has.Count.EqualTo(1));
            var created = client.CreatedJobs[0];
            Assert.That(created.Job.Type, Is.EqualTo(typeof(EmailJob)));
            Assert.That(created.Job.Method.Name, Is.EqualTo("ExecuteAsync"));
            Assert.That(created.Job.Args[0], Is.EqualTo(notificationId));
            Assert.That(created.State, Is.TypeOf<EnqueuedState>());
        });
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public List<(Job Job, IState State)> CreatedJobs { get; } = [];

        public string Create(Job job, IState state)
        {
            CreatedJobs.Add((job, state));
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState)
        {
            throw new NotSupportedException();
        }
    }
}
