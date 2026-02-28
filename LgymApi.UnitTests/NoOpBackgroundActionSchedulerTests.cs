using LgymApi.BackgroundWorker.Common;
using LgymApi.Infrastructure.Services;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class NoOpBackgroundActionSchedulerTests
{
    [Test]
    public void Enqueue_DoesNothing_NoExceptionThrown()
    {
        var scheduler = new NoOpActionMessageScheduler();
        var actionMessageId = Guid.NewGuid();

        Assert.DoesNotThrow(() => scheduler.Enqueue(actionMessageId));
    }

    [Test]
    public void Enqueue_CanBeCalledMultipleTimes()
    {
        var scheduler = new NoOpActionMessageScheduler();

        Assert.DoesNotThrow(() =>
        {
            scheduler.Enqueue(Guid.NewGuid());
            scheduler.Enqueue(Guid.NewGuid());
            scheduler.Enqueue(Guid.NewGuid());
        });
    }
}
