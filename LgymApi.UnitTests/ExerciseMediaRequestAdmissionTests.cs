using FluentAssertions;
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
        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeTrue();
        using var excess = firstHost.TryAcquire();
        excess.IsAcquired.Should().BeFalse();

        using var secondHost = new ExerciseMediaRequestAdmission();
        using var independent = secondHost.TryAcquire();
        independent.IsAcquired.Should().BeTrue();

        first.Dispose();
        using var next = firstHost.TryAcquire();
        next.IsAcquired.Should().BeTrue();
    }
}
