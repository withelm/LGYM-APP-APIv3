using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
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
        var actionMessageId = Id<CommandEnvelope>.New();

        scheduler.Enqueue(actionMessageId);

        Assert.That(client.CreatedJobs, Has.Exactly(1).Items);
        var created = client.CreatedJobs[0];
        Assert.That(created.Job.Type, Is.EqualTo(typeof(IActionMessageJob)));
        Assert.That(created.Job.Method.Name, Is.EqualTo("ExecuteAsync"));
        Assert.That(created.Job.Args[0], Is.EqualTo(actionMessageId.ToString()));
        Assert.That(created.State, Is.TypeOf<EnqueuedState>());
    }

    [Test]
    public void Enqueue_PassesOnlyStringId_NoPayloadObject()
    {
        var client = new FakeBackgroundJobClient();
        var scheduler = new HangfireActionMessageScheduler(client);
        var actionMessageId = Id<CommandEnvelope>.New();

        scheduler.Enqueue(actionMessageId);

        var created = client.CreatedJobs[0];
        Assert.That(created.Job.Args, Has.Exactly(1).Items);
        var assertedValue110 = created.Job.Args[0];
        Assert.That(assertedValue110, Is.TypeOf<string>());
        Assert.That(((string)assertedValue110), Is.EqualTo(actionMessageId.ToString()));
    }

    private sealed class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public List<(Job Job, IState State)> CreatedJobs { get; } = [];

        public string Create(Job job, IState state)
        {
            CreatedJobs.Add((job, state));
            return Id<FakeBackgroundJobClient>.New().ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState)
        {
            throw new NotSupportedException();
        }
    }
}
