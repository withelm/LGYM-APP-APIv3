using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CoachingBackgroundCommandContractTests
{
    private const string CommandsNamespace = "LgymApi.Application.Coaching.Contracts.BackgroundCommands";

    [TestCaseSource(nameof(CommandCases))]
    public void Command_Has_Exact_Public_Surface_Typed_Ids_And_Golden_Payload(ExpectedCommand expected)
    {
        var commandType = GetCommandType(expected.TypeName);
        var properties = commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.MetadataToken)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(commandType.Assembly, Is.SameAs(typeof(LgymApi.Application.ServiceCollectionExtensions).Assembly));
            Assert.That(commandType.Namespace, Is.EqualTo(CommandsNamespace));
            Assert.That(commandType.IsPublic, Is.True);
            Assert.That(commandType.IsSealed, Is.True);
            Assert.That(commandType.GetInterfaces(), Is.EqualTo(new[] { typeof(IActionCommand) }));
            Assert.That((commandType.GetConstructors()).Where(constructor => constructor.GetParameters().Length == 0), Has.Exactly(1).Items);
            Assert.That(properties.Select(property => property.Name), Is.EqualTo(expected.Properties.Select(property => property.Name)));
            Assert.That(commandType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly), Is.Empty);
            Assert.That(commandType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName), Is.Empty);
        });

        foreach (var expectedProperty in expected.Properties)
        {
            var property = properties.Single(candidate => candidate.Name == expectedProperty.Name);
            Assert.That(property.PropertyType, Is.EqualTo(expectedProperty.Type));
            Assert.That(property.GetMethod!.IsPublic, Is.True);
            Assert.That(property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers(), Does.Contain(typeof(IsExternalInit)));
        }

        var command = JsonSerializer.Deserialize(expected.PayloadJson, commandType, SharedSerializationOptions.Current);

        var assertedChain33 = command;
        Assert.That(assertedChain33, Is.Not.Null);
        Assert.That(assertedChain33, Is.TypeOf(commandType));
        Assert.That(JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current), Is.EqualTo(expected.PayloadJson));
    }

    [Test]
    public void TraineeNoteUpdatedInAppNotificationCommand_Preserves_Nullable_NoteTitle_And_Null_Omission()
    {
        var commandType = GetCommandType("TraineeNoteUpdatedInAppNotificationCommand");
        var noteTitle = commandType.GetProperty("NoteTitle", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        var nullPayload = "{\"traineeNoteId\":\"00000000-0000-0000-0000-000000000010\",\"traineeId\":\"00000000-0000-0000-0000-000000000011\",\"trainerId\":\"00000000-0000-0000-0000-000000000012\",\"triggeredAt\":\"2026-07-18T12:34:57+00:00\"}";

        Assert.That(new NullabilityInfoContext().Create(noteTitle).WriteState, Is.EqualTo(NullabilityState.Nullable));

        var command = JsonSerializer.Deserialize(nullPayload, commandType, SharedSerializationOptions.Current);

        Assert.That(command, Is.Not.Null);
        Assert.That(noteTitle.GetValue(command), Is.Null);
        Assert.That(JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current), Is.EqualTo(nullPayload));
    }

    [Test]
    public void CoachingCommands_UseTheFixedLegacyDeliveryChannelMap()
    {
        var registry = CommandContractRegistry.CreateDefault();

        foreach (var expected in DeliveryChannelCases)
        {
            var commandType = GetCommandType(expected.TypeName);
            var contract = registry.Contracts.Single(candidate => candidate.RuntimeType == commandType);
            var assertedValue86 = (contract.ExpectedHandlerTypes).ToArray();
            Assert.That(assertedValue86, Has.Exactly(1).Items);
            var handlerType = assertedValue86.Single();

            Assert.That(contract.CanonicalId, Is.EqualTo(expected.CanonicalId));
            Assert.That(contract.ReadAlias, Is.EqualTo($"{CommandsNamespace}.{expected.TypeName}"));
            Assert.That(handlerType.FullName, Is.EqualTo(expected.HandlerTypeFullName));
            Assert.That(ResolveDeliveryChannel(handlerType), Is.EqualTo(expected.Channel));

            if (expected.EmailPayloadTypeFullName is not null)
            {
                var emailPayloadType = GetEmailPayloadType(handlerType);
                Assert.That(emailPayloadType.FullName, Is.EqualTo(expected.EmailPayloadTypeFullName));
                var payload = (IEmailPayload)Activator.CreateInstance(emailPayloadType)!;
                Assert.That(payload.NotificationType.Value, Is.EqualTo(expected.DeliveryKey));
            }
        }
    }

    private static IEnumerable<TestCaseData> CommandCases()
    {
        yield return Case("InvitationCreatedCommand", "{\"invitationId\":\"00000000-0000-0000-0000-000000000004\"}", new ExpectedProperty("InvitationId", typeof(Id<TrainerInvitation>)));
        yield return Case("InvitationAcceptedCommand", "{\"invitationId\":\"00000000-0000-0000-0000-000000000005\"}", new ExpectedProperty("InvitationId", typeof(Id<TrainerInvitation>)));
        yield return Case("InvitationRevokedCommand", "{\"invitationId\":\"00000000-0000-0000-0000-000000000006\"}", new ExpectedProperty("InvitationId", typeof(Id<TrainerInvitation>)));
        yield return Case("TrainerInvitationCreatedInAppNotificationCommand", "{\"invitationId\":\"00000000-0000-0000-0000-000000000025\",\"traineeId\":\"00000000-0000-0000-0000-000000000026\",\"trainerId\":\"00000000-0000-0000-0000-000000000027\"}", new ExpectedProperty("InvitationId", typeof(Id<TrainerInvitation>)), new ExpectedProperty("TraineeId", typeof(Id<User>)), new ExpectedProperty("TrainerId", typeof(Id<User>)));
        yield return Case("TrainerInvitationAcceptedInAppNotificationCommand", "{\"invitationId\":\"00000000-0000-0000-0000-000000000022\",\"trainerId\":\"00000000-0000-0000-0000-000000000023\",\"traineeId\":\"00000000-0000-0000-0000-000000000024\"}", new ExpectedProperty("InvitationId", typeof(Id<TrainerInvitation>)), new ExpectedProperty("TrainerId", typeof(Id<User>)), new ExpectedProperty("TraineeId", typeof(Id<User>)));
        yield return Case("TrainerInvitationRejectedInAppNotificationCommand", "{\"invitationId\":\"00000000-0000-0000-0000-000000000028\",\"trainerId\":\"00000000-0000-0000-0000-000000000029\",\"traineeId\":\"00000000-0000-0000-0000-000000000030\"}", new ExpectedProperty("InvitationId", typeof(Id<TrainerInvitation>)), new ExpectedProperty("TrainerId", typeof(Id<User>)), new ExpectedProperty("TraineeId", typeof(Id<User>)));
        yield return Case("TrainerRelationshipEndedInAppNotificationCommand", "{\"trainerId\":\"00000000-0000-0000-0000-000000000031\",\"traineeId\":\"00000000-0000-0000-0000-000000000032\"}", new ExpectedProperty("TrainerId", typeof(Id<User>)), new ExpectedProperty("TraineeId", typeof(Id<User>)));
        yield return Case("TraineeNoteUpdatedInAppNotificationCommand", "{\"traineeNoteId\":\"00000000-0000-0000-0000-000000000010\",\"traineeId\":\"00000000-0000-0000-0000-000000000011\",\"trainerId\":\"00000000-0000-0000-0000-000000000012\",\"noteTitle\":\"Weekly check-in\",\"triggeredAt\":\"2026-07-18T12:34:57+00:00\"}", new ExpectedProperty("TraineeNoteId", typeof(Id<TraineeNote>)), new ExpectedProperty("TraineeId", typeof(Id<User>)), new ExpectedProperty("TrainerId", typeof(Id<User>)), new ExpectedProperty("NoteTitle", typeof(string)), new ExpectedProperty("TriggeredAt", typeof(DateTimeOffset)));
    }

    private static readonly DeliveryChannelContract[] DeliveryChannelCases =
    [
        new("InvitationCreatedCommand", "LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand", "LgymApi.BackgroundWorker.Actions.SendInvitationEmailHandler", DeliveryChannel.Email, "LgymApi.BackgroundWorker.Common.Notifications.Models.InvitationEmailPayload", "trainer.invitation.created"),
        new("TrainerInvitationCreatedInAppNotificationCommand", "LgymApi.BackgroundWorker.Common.Commands.TrainerInvitationCreatedInAppNotificationCommand", "LgymApi.BackgroundWorker.Actions.TrainerInvitationCreatedInAppNotificationCommandHandler", DeliveryChannel.InApp, null, null),
        new("InvitationAcceptedCommand", "LgymApi.BackgroundWorker.Common.Commands.InvitationAcceptedCommand", "LgymApi.BackgroundWorker.Actions.InvitationAcceptedEmailHandler", DeliveryChannel.Email, "LgymApi.BackgroundWorker.Common.Notifications.Models.InvitationAcceptedEmailPayload", "trainer.invitation.accepted"),
        new("TrainerInvitationAcceptedInAppNotificationCommand", "LgymApi.BackgroundWorker.Common.Commands.TrainerInvitationAcceptedInAppNotificationCommand", "LgymApi.BackgroundWorker.Actions.TrainerInvitationAcceptedInAppNotificationCommandHandler", DeliveryChannel.InApp, null, null),
        new("InvitationRevokedCommand", "LgymApi.BackgroundWorker.Common.Commands.InvitationRevokedCommand", "LgymApi.BackgroundWorker.Actions.InvitationRevokedEmailHandler", DeliveryChannel.Email, "LgymApi.BackgroundWorker.Common.Notifications.Models.InvitationRevokedEmailPayload", "trainer.invitation.revoked"),
        new("TrainerInvitationRejectedInAppNotificationCommand", "LgymApi.BackgroundWorker.Common.Commands.TrainerInvitationRejectedInAppNotificationCommand", "LgymApi.BackgroundWorker.Actions.TrainerInvitationRejectedInAppNotificationCommandHandler", DeliveryChannel.InApp, null, null),
        new("TrainerRelationshipEndedInAppNotificationCommand", "LgymApi.BackgroundWorker.Common.Commands.TrainerRelationshipEndedInAppNotificationCommand", "LgymApi.BackgroundWorker.Actions.TrainerRelationshipEndedInAppNotificationCommandHandler", DeliveryChannel.InApp, null, null),
        new("TraineeNoteUpdatedInAppNotificationCommand", "LgymApi.BackgroundWorker.Common.Commands.TraineeNoteUpdatedInAppNotificationCommand", "LgymApi.BackgroundWorker.Actions.TraineeNoteUpdatedInAppNotificationCommandHandler", DeliveryChannel.InApp, null, null)
    ];

    private static DeliveryChannel ResolveDeliveryChannel(Type handlerType)
    {
        var constructorParameterTypes = handlerType.GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        if (constructorParameterTypes.Any(type => type.FullName is "LgymApi.Application.Notifications.Contracts.Events.ICoachingEmailNotificationScheduler"
            or "LgymApi.BackgroundWorker.Common.Notifications.IEmailScheduler`1"
            or "LgymApi.Application.Coaching.Contracts.Notifications.IInvitationCreatedEmailPreparationPort"
            or "LgymApi.Application.Coaching.Contracts.Notifications.IInvitationAcceptedEmailPreparationPort"
            or "LgymApi.Application.Coaching.Contracts.Notifications.IInvitationRevokedEmailPreparationPort"))
        {
            return DeliveryChannel.Email;
        }

        if (constructorParameterTypes.Any(type => type.FullName is "LgymApi.Application.Notifications.IInAppNotificationService"
            or "LgymApi.Application.Notifications.Contracts.Events.ICoachingNotificationIntentService"
            or "LgymApi.Application.Coaching.Contracts.Notifications.ITrainerInvitationAcceptedInAppPreparationPort"
            or "LgymApi.Application.Coaching.Contracts.Notifications.ITrainerInvitationRejectedInAppPreparationPort"
            or "LgymApi.Application.Coaching.Contracts.Notifications.ITrainerInvitationCreatedInAppPreparationPort"
            or "LgymApi.Application.Coaching.Contracts.Notifications.ITraineeNoteUpdatedInAppPreparationPort"
            or "LgymApi.Application.Coaching.Contracts.Notifications.IRelationshipEndedPreparationPort"))
        {
            return DeliveryChannel.InApp;
        }

        throw new InvalidOperationException($"Handler '{handlerType.FullName}' has no supported legacy delivery dependency.");
    }

    private static Type GetEmailPayloadType(Type handlerType) => handlerType.FullName switch
    {
        "LgymApi.BackgroundWorker.Actions.SendInvitationEmailHandler" => typeof(InvitationEmailPayload),
        "LgymApi.BackgroundWorker.Actions.InvitationAcceptedEmailHandler" => typeof(InvitationAcceptedEmailPayload),
        "LgymApi.BackgroundWorker.Actions.InvitationRevokedEmailHandler" => typeof(InvitationRevokedEmailPayload),
        _ => throw new InvalidOperationException($"Handler '{handlerType.FullName}' has no legacy email payload mapping.")
    };

    private static TestCaseData Case(string typeName, string payloadJson, params ExpectedProperty[] properties) =>
        new TestCaseData(new ExpectedCommand(typeName, payloadJson, properties)).SetName(typeName);

    private static Type GetCommandType(string typeName)
    {
        var commandType = typeof(LgymApi.Application.ServiceCollectionExtensions).Assembly.GetType($"{CommandsNamespace}.{typeName}");
        Assert.That(commandType, Is.Not.Null, $"{typeName} must be owned by the Coaching Application module");
        return commandType!;
    }

    public sealed record ExpectedCommand(string TypeName, string PayloadJson, ExpectedProperty[] Properties);

    public sealed record ExpectedProperty(string Name, Type Type);

    private sealed record DeliveryChannelContract(
        string TypeName,
        string CanonicalId,
        string HandlerTypeFullName,
        DeliveryChannel Channel,
        string? EmailPayloadTypeFullName,
        string? DeliveryKey);

    private enum DeliveryChannel
    {
        Email,
        InApp
    }
}
