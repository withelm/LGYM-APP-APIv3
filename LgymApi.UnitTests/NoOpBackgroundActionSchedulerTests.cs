using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class NoOpBackgroundActionSchedulerTests
{
    [Test]
    public void Enqueue_DoesNothing_NoExceptionThrown()
    {
        var scheduler = new NoOpActionMessageScheduler();
        var actionMessageId = Id<CommandEnvelope>.New();

        Assert.DoesNotThrow(() => scheduler.Enqueue(actionMessageId));
    }

    [Test]
    public void Enqueue_CanBeCalledMultipleTimes()
    {
        var scheduler = new NoOpActionMessageScheduler();

        Assert.DoesNotThrow(() =>
        {
            scheduler.Enqueue(Id<CommandEnvelope>.New());
            scheduler.Enqueue(Id<CommandEnvelope>.New());
            scheduler.Enqueue(Id<CommandEnvelope>.New());
        });
    }
}
