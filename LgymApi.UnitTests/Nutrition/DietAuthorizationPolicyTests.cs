using FluentAssertions;
using LgymApi.Application.Nutrition.DietPlans;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Platform.Contracts;

namespace LgymApi.UnitTests.Nutrition;

[TestFixture]
public sealed class DietAuthorizationPolicyTests
{
    [Test]
    public void IsOwnedByTrainee_IgnoresOriginalTrainerButRejectsOtherTraineesAndDeletedPlans()
    {
        var traineeId = Id<User>.New();
        var plan = new DietPlan { TrainerId = Id<User>.New(), TraineeId = traineeId };

        DietPlanAccess.IsOwnedByTrainee(plan, traineeId).Should().BeTrue();
        DietPlanAccess.IsOwnedByTrainee(plan, Id<User>.New()).Should().BeFalse();
        plan.IsDeleted = true;
        DietPlanAccess.IsOwnedByTrainee(plan, traineeId).Should().BeFalse();
    }

    [Test]
    public void Policy_AllowsOwnerReadHistoryAndUpdate_ButNotCreateActivateOrDelete()
    {
        var ownerId = Id<User>.New();
        var actor = new AuthorizationActor(ownerId.Rebind<ActorReference>());
        var policy = new DietPlanAuthorizationPolicy();

        policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Read)).IsAllowed.Should().BeTrue();
        policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.ReadHistory)).IsAllowed.Should().BeTrue();
        policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Update)).IsAllowed.Should().BeTrue();
        policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Create)).IsAllowed.Should().BeFalse();
        policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Activate)).IsAllowed.Should().BeFalse();
        policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Delete)).IsAllowed.Should().BeFalse();
    }
}
