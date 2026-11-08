using FluentAssertions;
using LgymApi.Application.Notifications.Authorization;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Platform.Contracts;

namespace LgymApi.Notifications.UnitTests;

[TestFixture]
public sealed class NotificationOwnershipPolicyTests
{
    [Test]
    public void Evaluate_AllowsTheNotificationRecipient()
    {
        var recipientId = Id<ActorReference>.New();
        var actor = new AuthorizationActor(recipientId);
        var policy = new NotificationOwnershipPolicy();

        var decision = policy.Evaluate(actor, new NotificationOwnershipContext(recipientId));

        decision.Should().Be(AuthorizationDecision.Allow);
    }

    [Test]
    public void Evaluate_DeniesAnotherActor()
    {
        var actor = new AuthorizationActor(Id<ActorReference>.New());
        var context = new NotificationOwnershipContext(Id<ActorReference>.New());
        var policy = new NotificationOwnershipPolicy();

        var decision = policy.Evaluate(actor, context);

        decision.Should().Be(AuthorizationDecision.Deny);
    }
}
