using System.Text.Json;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Notifications.Contracts;
using LgymApi.Platform.Contracts;
using LgymApi.TrainingPlanning.Contracts;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ModuleReferenceMarkerTests
{
    private sealed class SourceScope;

    private static Id<T> ParseTestId<T>(string uuid)
    {
        if (!Id<T>.TryParse(uuid, out Id<T> id))
        {
            throw new ArgumentException($"Invalid UUID: {uuid}", nameof(uuid));
        }

        return id;
    }

    [Test]
    public void Rebind_PreservesValueWithinEachApprovedMarkerScope()
    {
        Id<SourceScope> source = ParseTestId<SourceScope>("00000000-0000-0000-0000-000000000004");

        var actor = source.Rebind<ActorReference>();
        var account = source.Rebind<AccountReference>();
        var accountSession = source.Rebind<AccountSessionReference>();
        var role = source.Rebind<RoleReference>();
        var plan = source.Rebind<PlanReference>();
        var planDay = source.Rebind<PlanDayReference>();
        var planExercise = source.Rebind<PlanExerciseReference>();
        var notification = source.Rebind<NotificationReference>();
        var pushInstallation = source.Rebind<PushInstallationReference>();

        Assert.Multiple(() =>
        {
            Assert.That(actor, Is.EqualTo(source.Rebind<ActorReference>()));
            Assert.That(account, Is.EqualTo(source.Rebind<AccountReference>()));
            Assert.That(accountSession, Is.EqualTo(source.Rebind<AccountSessionReference>()));
            Assert.That(role, Is.EqualTo(source.Rebind<RoleReference>()));
            Assert.That(plan, Is.EqualTo(source.Rebind<PlanReference>()));
            Assert.That(planDay, Is.EqualTo(source.Rebind<PlanDayReference>()));
            Assert.That(planExercise, Is.EqualTo(source.Rebind<PlanExerciseReference>()));
            Assert.That(notification, Is.EqualTo(source.Rebind<NotificationReference>()));
            Assert.That(pushInstallation, Is.EqualTo(source.Rebind<PushInstallationReference>()));
            Assert.That(account.Equals((object)plan), Is.False);
        });
    }

    [Test]
    public void Rebind_PreservesTargetScopeHashAndJsonSerialization()
    {
        Id<SourceScope> source = ParseTestId<SourceScope>("00000000-0000-0000-0000-000000000005");
        Id<AccountReference> account = source.Rebind<AccountReference>();

        var json = JsonSerializer.Serialize(account, SharedSerializationOptions.Current);
        var roundTrip = JsonSerializer.Deserialize<Id<AccountReference>>(json, SharedSerializationOptions.Current);

        Assert.Multiple(() =>
        {
            Assert.That(account.GetHashCode(), Is.EqualTo(source.Rebind<AccountReference>().GetHashCode()));
            Assert.That(json, Is.EqualTo("\"00000000-0000-0000-0000-000000000005\""));
            Assert.That(roundTrip, Is.EqualTo(account));
        });
    }
}
