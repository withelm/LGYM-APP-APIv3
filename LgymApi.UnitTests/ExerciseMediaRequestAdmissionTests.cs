using LgymApi.Api.Features.Exercise.Filters;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseMediaRequestAdmissionTests
{
    [Test]
    public void Intake_IsBounded_ReleasedOnDispose_AndIndependentBetweenHosts()
    {
        using var firstHost = new ExerciseMediaRequestAdmission();
        using var first = firstHost.TryAcquire();
        using var second = firstHost.TryAcquire();
        Assert.That(first.IsAcquired, Is.True);
        Assert.That(second.IsAcquired, Is.True);
        using var excess = firstHost.TryAcquire();
        Assert.That(excess.IsAcquired, Is.False);

        using var secondHost = new ExerciseMediaRequestAdmission();
        using var independent = secondHost.TryAcquire();
        Assert.That(independent.IsAcquired, Is.True);

        first.Dispose();
        using var next = firstHost.TryAcquire();
        Assert.That(next.IsAcquired, Is.True);
    }
}
