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

        Assert.That(DietPlanAccess.IsOwnedByTrainee(plan, traineeId), Is.True);
        Assert.That(DietPlanAccess.IsOwnedByTrainee(plan, Id<User>.New()), Is.False);
        plan.IsDeleted = true;
        Assert.That(DietPlanAccess.IsOwnedByTrainee(plan, traineeId), Is.False);
    }

    [Test]
    public void Policy_AllowsOwnerReadHistoryAndUpdate_ButNotCreateActivateOrDelete()
    {
        var ownerId = Id<User>.New();
        var actor = new AuthorizationActor(ownerId.Rebind<ActorReference>());
        var policy = new DietPlanAuthorizationPolicy();

        Assert.That(policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Read)).IsAllowed, Is.True);
        Assert.That(policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.ReadHistory)).IsAllowed, Is.True);
        Assert.That(policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Update)).IsAllowed, Is.True);
        Assert.That(policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Create)).IsAllowed, Is.False);
        Assert.That(policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Activate)).IsAllowed, Is.False);
        Assert.That(policy.Evaluate(actor, new DietPlanAuthorizationContext(ownerId.Rebind<ActorReference>(), false, false, DietPlanAction.Delete)).IsAllowed, Is.False);
    }
}
