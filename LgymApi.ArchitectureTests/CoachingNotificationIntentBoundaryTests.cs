using System.Reflection;
using LgymApi.Application.Coaching.Contracts.Notifications;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.Domain.Entities;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CoachingNotificationIntentBoundaryTests
{
    private static readonly Type[] IntentTypes =
    [
        typeof(InvitationCreatedCoachingNotificationIntent),
        typeof(InvitationAcceptedCoachingNotificationIntent),
        typeof(InvitationRejectedCoachingNotificationIntent),
        typeof(InvitationRevokedCoachingNotificationIntent),
        typeof(RelationshipEndedCoachingNotificationIntent),
        typeof(TraineeNoteUpdatedCoachingNotificationIntent)
    ];

    [Test]
    public void CoachingNotificationIntents_ExposeExactlySixFactOnlyRecords()
    {
        var contractsAssembly = typeof(ICoachingNotificationIntentService).Assembly;
        var discovered = contractsAssembly.GetTypes()
            .Where(type => type.Namespace == typeof(CoachingNotificationIntent).Namespace)
            .Where(type => type.IsSealed && type.IsSubclassOf(typeof(CoachingNotificationIntent)))
            .OrderBy(type => type.Name)
            .ToArray();

        Assert.That(discovered, Is.EquivalentTo(IntentTypes).UsingPropertiesComparer());
        foreach (var intentType in IntentTypes)
        {
            Assert.That((intentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)).Any(property => property.Name.Contains("Message", StringComparison.Ordinal)), Is.False);
            Assert.That((intentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)).Any(property =>
                    property.Name.Contains("Token", StringComparison.Ordinal) ||
                    property.Name.Contains("Payload", StringComparison.Ordinal) ||
                    property.Name.Contains("Repository", StringComparison.Ordinal)), Is.False);
            Assert.That((intentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.PropertyType.FullName)).Any(name => name!.Contains("BackgroundWorker", StringComparison.Ordinal) || name.Contains("Common", StringComparison.Ordinal)), Is.False);
            Assert.That((intentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.PropertyType)).Any(type => type == typeof(NotificationMessage) || type == typeof(PushInstallation) || type == typeof(PushNotificationMessage)), Is.False);
            Assert.That((intentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.PropertyType)).Any(type => type == typeof(User) || type == typeof(TrainerInvitation) || type == typeof(TraineeNote)), Is.False);
        }
    }

    [Test]
    public void CoachingNotificationReadContract_ExposesOnlyTypedInvitationFacts()
    {
        var assertedValue2 = (typeof(ICoachingNotificationReadService).GetMethods(BindingFlags.Public | BindingFlags.Instance)).ToArray();
        Assert.That(assertedValue2, Has.Exactly(1).Items);
        var method = assertedValue2.Single();

        Assert.That(method.ReturnType.FullName, Does.Not.Contain("BackgroundWorker"));
        Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType), Does.Contain(typeof(LgymApi.Domain.ValueObjects.Id<TrainerInvitation>)));
        Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType), Does.Not.Contain(typeof(TrainerInvitation)));
    }
}
