using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class HangfireBackgroundActionSchedulerTests
{
    [Test]
    public void Enqueue_CreatesHangfireJob_ForActionMessageJob()
    {
        var client = new FakeBackgroundJobClient();
        var scheduler = new HangfireActionMessageScheduler(client);
        var actionMessageId = Guid.NewGuid();

        scheduler.Enqueue(actionMessageId);

        Assert.Multiple(() =>
        {
            Assert.That(client.CreatedJobs, Has.Count.EqualTo(1));
            var created = client.CreatedJobs[0];
            Assert.That(created.Job.Type, Is.EqualTo(typeof(IActionMessageJob)));
            Assert.That(created.Job.Method.Name, Is.EqualTo("ExecuteAsync"));
            Assert.That(created.Job.Args[0], Is.EqualTo(actionMessageId));
            Assert.That(created.State, Is.TypeOf<EnqueuedState>());
        });
    }

    [Test]
    public void Enqueue_PassesOnlyGuid_NoPayloadObject()
    {
        var client = new FakeBackgroundJobClient();
        var scheduler = new HangfireActionMessageScheduler(client);
        var actionMessageId = Guid.NewGuid();

        scheduler.Enqueue(actionMessageId);

        var created = client.CreatedJobs[0];
        Assert.Multiple(() =>
        {
            Assert.That(created.Job.Args, Has.Count.EqualTo(1));
            Assert.That(created.Job.Args[0], Is.TypeOf<Guid>());
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
