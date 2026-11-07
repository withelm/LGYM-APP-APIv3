using FluentAssertions;
using LgymApi.Application.WorkoutProgress.Authorization;
using LgymApi.Authorization;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Platform.Contracts;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class WorkoutProgressOwnershipAuthorizationTests
{
    [Test]
    public void IsOwner_AllowsOnlyTheMatchingNonEmptyActor()
    {
        var ownerId = Id<AccountReference>.New();
        var service = CreateService();

        service.IsOwner(ownerId, ownerId).Should().BeTrue();
        service.IsOwner(Id<AccountReference>.New(), ownerId).Should().BeFalse();
        service.IsOwner(Id<AccountReference>.Empty, ownerId).Should().BeFalse();
        service.IsOwner(ownerId, Id<AccountReference>.Empty).Should().BeFalse();
    }

    [Test]
    public void Policy_DeniesAnUnrelatedSystemActor()
    {
        var evaluator = new AuthorizationPolicyEvaluator<WorkoutProgressOwnershipAuthorizationContext>(
            [new WorkoutProgressOwnershipAuthorizationPolicy()]);

        var result = evaluator.Evaluate(
            new AuthorizationActor(Id<ActorReference>.New()),
            new WorkoutProgressOwnershipAuthorizationContext(Id<ActorReference>.New()));

        result.Should().Be(AuthorizationDecision.Deny);
    }

    private static IWorkoutProgressOwnershipAuthorizationService CreateService()
        => new WorkoutProgressOwnershipAuthorizationService(
            new AuthorizationPolicyEvaluator<WorkoutProgressOwnershipAuthorizationContext>(
                [new WorkoutProgressOwnershipAuthorizationPolicy()]));
}
