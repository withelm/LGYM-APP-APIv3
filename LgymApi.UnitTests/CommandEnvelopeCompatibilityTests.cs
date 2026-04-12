using FluentAssertions;
using LgymApi.Domain.ValueObjects;
using System.Text.Json;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using NUnit.Framework;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests CommandEnvelope persisted payload compatibility with current serialization options.
/// Validates that persisted envelopes (with various discriminator + payload combinations) deserialize successfully.
/// Covers backward compatibility for legacy payloads and discriminator resolution after serialization changes.
/// </summary>
[TestFixture]
public sealed class CommandEnvelopeCompatibilityTests
{
    #region UserRegisteredCommand Compatibility Tests

     [Test]
     public void CommandEnvelope_UserRegisteredCommand_DeserializesFromPersistedCamelCasePayload()
     {
         // Arrange - simulate persisted envelope with camelCase payload
         var userId = Id<User>.New();
         var persistedPayloadJson = $$"""{"userId":"{{userId}}"}""";
         var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand";

         // Act - resolve type and deserialize payload
         var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
         var deserializedCommand = JsonSerializer.Deserialize(persistedPayloadJson, resolvedType, SharedSerializationOptions.Current);

         // Assert - exact type and payload field preservation
         resolvedType.Should().Be(typeof(UserRegisteredCommand));
         deserializedCommand.Should().NotBeNull();
         deserializedCommand.Should().BeOfType<UserRegisteredCommand>();
         
         var command = (UserRegisteredCommand)deserializedCommand!;
         command.UserId.Should().Be(userId);
     }

     [Test]
     public void CommandEnvelope_UserRegisteredCommand_DeserializesFromPersistedPascalCasePayload()
     {
         // Arrange - simulate legacy persisted envelope with PascalCase payload
         var userId = Id<User>.New();
         var legacyPayloadJson = $$"""{"UserId":"{{userId}}"}""";
         var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand";

         // Act - resolve type and deserialize legacy payload
         var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
         var deserializedCommand = JsonSerializer.Deserialize(legacyPayloadJson, resolvedType, SharedSerializationOptions.Current);

         // Assert - PropertyNameCaseInsensitive enables legacy payload compatibility
         resolvedType.Should().Be(typeof(UserRegisteredCommand));
         deserializedCommand.Should().NotBeNull();
         
         var command = (UserRegisteredCommand)deserializedCommand!;
         command.UserId.Should().Be(userId);
     }

     [Test]
     public void CommandEnvelope_UserRegisteredCommand_RoundTripPreservesPayloadAndDiscriminator()
     {
         // Arrange - create command and simulate full envelope lifecycle
         var userId = Id<User>.New();
         var command = new UserRegisteredCommand { UserId = userId };
         var commandType = typeof(UserRegisteredCommand);

         // Act - serialize payload and discriminator (as CommandDispatcher does)
         var payloadJson = JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);
         var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);

         // Simulate persistence and retrieval
         var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);
         var deserializedCommand = JsonSerializer.Deserialize(payloadJson, resolvedType, SharedSerializationOptions.Current);

         // Assert - full roundtrip preserves type and payload
         resolvedType.Should().Be(commandType);
         deserializedCommand.Should().NotBeNull();
         
         var roundTrippedCommand = (UserRegisteredCommand)deserializedCommand!;
         roundTrippedCommand.UserId.Should().Be(userId);
         
         // Verify serialized payload uses camelCase
         payloadJson.Should().Contain("userId");
         payloadJson.Should().NotContain("UserId");
     }

    #endregion

    #region TrainingCompletedCommand Compatibility Tests

     [Test]
     public void CommandEnvelope_TrainingCompletedCommand_DeserializesFromPersistedCamelCasePayload()
     {
         // Arrange - simulate persisted envelope with camelCase payload
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();
         var persistedPayloadJson = $$"""{"userId":"{{userId}}","trainingId":"{{trainingId}}"}""";
         var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand";

         // Act - resolve type and deserialize payload
         var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
         var deserializedCommand = JsonSerializer.Deserialize(persistedPayloadJson, resolvedType, SharedSerializationOptions.Current);

         // Assert - exact type and all payload fields preserved
         resolvedType.Should().Be(typeof(TrainingCompletedCommand));
         deserializedCommand.Should().NotBeNull();
         
         var command = (TrainingCompletedCommand)deserializedCommand!;
         command.UserId.Should().Be(userId);
         command.TrainingId.Should().Be(trainingId);
     }

     [Test]
     public void CommandEnvelope_TrainingCompletedCommand_DeserializesFromPersistedPascalCasePayload()
     {
         // Arrange - simulate legacy persisted envelope with PascalCase payload
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();
         var legacyPayloadJson = $$"""{"UserId":"{{userId}}","TrainingId":"{{trainingId}}"}""";
         var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand";

         // Act - resolve type and deserialize legacy payload
         var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
         var deserializedCommand = JsonSerializer.Deserialize(legacyPayloadJson, resolvedType, SharedSerializationOptions.Current);

         // Assert - PropertyNameCaseInsensitive enables legacy payload compatibility
         resolvedType.Should().Be(typeof(TrainingCompletedCommand));
         deserializedCommand.Should().NotBeNull();
         
         var command = (TrainingCompletedCommand)deserializedCommand!;
         command.UserId.Should().Be(userId);
         command.TrainingId.Should().Be(trainingId);
     }

     [Test]
     public void CommandEnvelope_TrainingCompletedCommand_RoundTripPreservesAllFields()
     {
         // Arrange - create command with multiple properties
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();
         var command = new TrainingCompletedCommand { UserId = userId, TrainingId = trainingId };
         var commandType = typeof(TrainingCompletedCommand);

         // Act - serialize and deserialize
         var payloadJson = JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);
         var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);
         var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);
         var deserializedCommand = JsonSerializer.Deserialize(payloadJson, resolvedType, SharedSerializationOptions.Current);

         // Assert - all fields preserved through roundtrip
         resolvedType.Should().Be(commandType);
         var roundTrippedCommand = (TrainingCompletedCommand)deserializedCommand!;
         roundTrippedCommand.UserId.Should().Be(userId);
         roundTrippedCommand.TrainingId.Should().Be(trainingId);
         
         // Verify serialized payload uses camelCase
         payloadJson.Should().Contain("userId");
         payloadJson.Should().Contain("trainingId");
     }

    #endregion

    #region InvitationCreatedCommand Compatibility Tests

     [Test]
     public void CommandEnvelope_InvitationCreatedCommand_DeserializesFromPersistedCamelCasePayload()
     {
         // Arrange - simulate persisted envelope with camelCase payload
         var invitationId = Id<TrainerInvitation>.New();
         var persistedPayloadJson = $$"""{"invitationId":"{{invitationId}}"}""";
         var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand";

         // Act - resolve type and deserialize payload
         var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
         var deserializedCommand = JsonSerializer.Deserialize(persistedPayloadJson, resolvedType, SharedSerializationOptions.Current);

         // Assert - exact type and payload field preservation
         resolvedType.Should().Be(typeof(InvitationCreatedCommand));
         deserializedCommand.Should().NotBeNull();
         
         var command = (InvitationCreatedCommand)deserializedCommand!;
         command.InvitationId.Should().Be(invitationId);
     }

     [Test]
     public void CommandEnvelope_InvitationCreatedCommand_DeserializesFromPersistedPascalCasePayload()
     {
         // Arrange - simulate legacy persisted envelope with PascalCase payload
         var invitationId = Id<TrainerInvitation>.New();
         var legacyPayloadJson = $$"""{"InvitationId":"{{invitationId}}"}""";
         var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand";

         // Act - resolve type and deserialize legacy payload
         var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
         var deserializedCommand = JsonSerializer.Deserialize(legacyPayloadJson, resolvedType, SharedSerializationOptions.Current);

         // Assert - PropertyNameCaseInsensitive enables legacy payload compatibility
         resolvedType.Should().Be(typeof(InvitationCreatedCommand));
         deserializedCommand.Should().NotBeNull();
         
         var command = (InvitationCreatedCommand)deserializedCommand!;
         command.InvitationId.Should().Be(invitationId);
     }

     [Test]
     public void CommandEnvelope_InvitationCreatedCommand_RoundTripPreservesPayloadAndDiscriminator()
     {
         // Arrange - create command
         var invitationId = Id<TrainerInvitation>.New();
         var command = new InvitationCreatedCommand { InvitationId = invitationId };
         var commandType = typeof(InvitationCreatedCommand);

         // Act - serialize and deserialize
         var payloadJson = JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);
         var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);
         var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);
         var deserializedCommand = JsonSerializer.Deserialize(payloadJson, resolvedType, SharedSerializationOptions.Current);

         // Assert - full roundtrip preserves type and payload
         resolvedType.Should().Be(commandType);
         var roundTrippedCommand = (InvitationCreatedCommand)deserializedCommand!;
         roundTrippedCommand.InvitationId.Should().Be(invitationId);
         
         // Verify serialized payload uses camelCase
         payloadJson.Should().Contain("invitationId");
         payloadJson.Should().NotContain("InvitationId");
     }

    #endregion

    #region Cross-Command Compatibility Tests

     [Test]
     public void CommandEnvelope_AllKnownCommands_DeserializeFromPersistedEnvelopes()
     {
         // Arrange - simulate persisted envelopes for all known commands
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();
         var invitationId = Id<TrainerInvitation>.New();

         var persistedEnvelopes = new[]
         {
             new
             {
                 PayloadJson = $$"""{"userId":"{{userId}}"}""",
                 Discriminator = "LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand",
                 ExpectedType = typeof(UserRegisteredCommand)
             },
             new
             {
                 PayloadJson = $$"""{"userId":"{{userId}}","trainingId":"{{trainingId}}"}""",
                 Discriminator = "LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand",
                 ExpectedType = typeof(TrainingCompletedCommand)
             },
             new
             {
                 PayloadJson = $$"""{"invitationId":"{{invitationId}}"}""",
                 Discriminator = "LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand",
                 ExpectedType = typeof(InvitationCreatedCommand)
             }
         };

         // Act & Assert - all persisted envelopes deserialize correctly
         foreach (var envelope in persistedEnvelopes)
         {
             var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(envelope.Discriminator);
             var deserializedCommand = JsonSerializer.Deserialize(envelope.PayloadJson, resolvedType, SharedSerializationOptions.Current);

             resolvedType.Should().Be(envelope.ExpectedType, 
                 $"Failed to resolve type for discriminator: {envelope.Discriminator}");
             deserializedCommand.Should().NotBeNull(
                 $"Failed to deserialize payload for discriminator: {envelope.Discriminator}");
             deserializedCommand.Should().BeOfType(envelope.ExpectedType,
                 $"Deserialized command is not of expected type: {envelope.ExpectedType}");
         }
     }

     [Test]
     public void CommandEnvelope_MixedCasePayloads_AllDeserializeSuccessfully()
     {
         // Arrange - simulate mixed legacy and current payloads
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();

         var mixedPayloads = new[]
         {
             // Legacy PascalCase
             new
             {
                 PayloadJson = $$"""{"UserId":"{{userId}}"}""",
                 Discriminator = "LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand",
                 ExpectedType = typeof(UserRegisteredCommand)
             },
             // Current camelCase
             new
             {
                 PayloadJson = $$"""{"userId":"{{userId}}","trainingId":"{{trainingId}}"}""",
                 Discriminator = "LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand",
                 ExpectedType = typeof(TrainingCompletedCommand)
             },
             // Mixed case (should work due to PropertyNameCaseInsensitive)
             new
             {
                 PayloadJson = $$"""{"UserId":"{{userId}}","trainingId":"{{trainingId}}"}""",
                 Discriminator = "LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand",
                 ExpectedType = typeof(TrainingCompletedCommand)
             }
         };

         // Act & Assert - all mixed-case payloads deserialize without error
         foreach (var payload in mixedPayloads)
         {
             var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(payload.Discriminator);
             var deserializedCommand = JsonSerializer.Deserialize(payload.PayloadJson, resolvedType, SharedSerializationOptions.Current);

             deserializedCommand.Should().NotBeNull(
                 $"Failed to deserialize mixed-case payload: {payload.PayloadJson}");
             deserializedCommand.Should().BeOfType(payload.ExpectedType,
                 $"Deserialized command is not of expected type: {payload.ExpectedType}");
         }
     }

     [Test]
     public void CommandEnvelope_FullLifecycleSimulation_PreservesPayloadIntegrity()
     {
         // Arrange - simulate full CommandEnvelope lifecycle for all known commands
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();
         var invitationId = Id<TrainerInvitation>.New();

         var commands = new IActionCommand[]
         {
             new UserRegisteredCommand { UserId = userId },
             new TrainingCompletedCommand { UserId = userId, TrainingId = trainingId },
             new InvitationCreatedCommand { InvitationId = invitationId }
         };

         // Act - simulate CommandDispatcher serialization and persistence
         foreach (var command in commands)
         {
             var commandType = command.GetType();
             
             // Step 1: Serialize payload and generate discriminator (CommandDispatcher)
             var payloadJson = JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);
             var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);

             // Step 2: Persist envelope (simulated database storage)
             var envelope = new CommandEnvelope
             {
                 Id = Id<CommandEnvelope>.New(),
                 CorrelationId = Id<CorrelationScope>.New(),
                 PayloadJson = payloadJson,
                 CommandTypeFullName = discriminator,
                 Status = ActionExecutionStatus.Pending,
                 CreatedAt = DateTimeOffset.UtcNow,
                 UpdatedAt = DateTimeOffset.UtcNow
             };

             // Step 3: Retrieve and deserialize (BackgroundActionOrchestratorService)
             var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(envelope.CommandTypeFullName);
             var deserializedCommand = JsonSerializer.Deserialize(envelope.PayloadJson, resolvedType, SharedSerializationOptions.Current);

             // Assert - full lifecycle preserves type and payload
             resolvedType.Should().Be(commandType);
             deserializedCommand.Should().NotBeNull();
             deserializedCommand.Should().BeOfType(commandType);
             
             // Verify specific field values based on command type
             switch (deserializedCommand)
             {
                 case UserRegisteredCommand urc:
                     urc.UserId.Should().Be(userId);
                     break;
                 case TrainingCompletedCommand tcc:
                     tcc.UserId.Should().Be(userId);
                     tcc.TrainingId.Should().Be(trainingId);
                     break;
                 case InvitationCreatedCommand icc:
                     icc.InvitationId.Should().Be(invitationId);
                     break;
             }
         }
     }

    #endregion
}
