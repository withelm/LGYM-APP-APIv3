using FluentAssertions;
using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.Adapters;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Identity.Contracts.Access;
using LgymApi.Application.WorkoutProgress.Contracts.Measurements;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MeasurementsRelationshipAccessAdapterTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task HasActiveRelationshipAsync_ReturnsCoachingRelationshipDecision(bool hasActiveRelationship)
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var relationshipAccess = Substitute.For<ICoachingRelationshipAccessService>();
        relationshipAccess.GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None)
            .Returns(new CoachingRelationshipAccessDecision(hasActiveRelationship, hasActiveRelationship));
        var adapter = new MeasurementsRelationshipAccessAdapter(relationshipAccess);

        var result = await adapter.HasActiveRelationshipAsync(trainerId, traineeId);

        result.Should().Be(hasActiveRelationship);
        await relationshipAccess.Received(1)
            .GetAccessDecisionAsync(trainerId, traineeId, CancellationToken.None);
    }

    [Test]
    public async Task HasActiveRelationshipAsync_ForwardsTypedIdsAndCancellation()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var relationshipAccess = Substitute.For<ICoachingRelationshipAccessService>();
        relationshipAccess.GetAccessDecisionAsync(trainerId, traineeId, cancellationToken)
            .Returns(new CoachingRelationshipAccessDecision(true, false));
        var adapter = new MeasurementsRelationshipAccessAdapter(relationshipAccess);

        await adapter.HasActiveRelationshipAsync(trainerId, traineeId, cancellationToken);

        await relationshipAccess.Received(1)
            .GetAccessDecisionAsync(trainerId, traineeId, cancellationToken);
    }

    [Test]
    public void AddCoachingModule_RegistersMeasurementsRelationshipAccessAdapterExactlyOnceAndResolvesIt()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Substitute.For<IUserAccessReadService>());
        services.AddScoped(_ => Substitute.For<ICoachingActiveLinkPersistence>());
        services.AddCoachingModule();

        services.Count(descriptor => descriptor.ServiceType == typeof(IMeasurementsRelationshipAccessPort))
            .Should()
            .Be(1);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetServices<IMeasurementsRelationshipAccessPort>()
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<MeasurementsRelationshipAccessAdapter>();
    }
}
