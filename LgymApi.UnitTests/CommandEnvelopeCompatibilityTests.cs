using System.Text.Json;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

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
        var userId = Guid.NewGuid();
        var persistedPayloadJson = $$"""{"userId":"{{userId}}"}""";
        var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand";

        // Act - resolve type and deserialize payload
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
        var deserializedCommand = JsonSerializer.Deserialize(persistedPayloadJson, resolvedType, SharedSerializationOptions.Current);

        // Assert - exact type and payload field preservation
        Assert.That(resolvedType, Is.EqualTo(typeof(UserRegisteredCommand)));
        Assert.That(deserializedCommand, Is.Not.Null);
        Assert.That(deserializedCommand, Is.InstanceOf<UserRegisteredCommand>());
        
        var command = (UserRegisteredCommand)deserializedCommand!;
        Assert.That(command.UserId, Is.EqualTo(userId));
    }

    [Test]
    public void CommandEnvelope_UserRegisteredCommand_DeserializesFromPersistedPascalCasePayload()
    {
        // Arrange - simulate legacy persisted envelope with PascalCase payload
        var userId = Guid.NewGuid();
        var legacyPayloadJson = $$"""{"UserId":"{{userId}}"}""";
        var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.UserRegisteredCommand";

        // Act - resolve type and deserialize legacy payload
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
        var deserializedCommand = JsonSerializer.Deserialize(legacyPayloadJson, resolvedType, SharedSerializationOptions.Current);

        // Assert - PropertyNameCaseInsensitive enables legacy payload compatibility
        Assert.That(resolvedType, Is.EqualTo(typeof(UserRegisteredCommand)));
        Assert.That(deserializedCommand, Is.Not.Null);
        
        var command = (UserRegisteredCommand)deserializedCommand!;
        Assert.That(command.UserId, Is.EqualTo(userId));
    }

    [Test]
    public void CommandEnvelope_UserRegisteredCommand_RoundTripPreservesPayloadAndDiscriminator()
    {
        // Arrange - create command and simulate full envelope lifecycle
        var userId = Guid.NewGuid();
        var command = new UserRegisteredCommand { UserId = userId };
        var commandType = typeof(UserRegisteredCommand);

        // Act - serialize payload and discriminator (as CommandDispatcher does)
        var payloadJson = JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);

        // Simulate persistence and retrieval
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);
        var deserializedCommand = JsonSerializer.Deserialize(payloadJson, resolvedType, SharedSerializationOptions.Current);

        // Assert - full roundtrip preserves type and payload
        Assert.That(resolvedType, Is.EqualTo(commandType));
        Assert.That(deserializedCommand, Is.Not.Null);
        
        var roundTrippedCommand = (UserRegisteredCommand)deserializedCommand!;
        Assert.That(roundTrippedCommand.UserId, Is.EqualTo(userId));
        
        // Verify serialized payload uses camelCase
        Assert.That(payloadJson, Does.Contain("userId"));
        Assert.That(payloadJson, Does.Not.Contain("UserId"));
    }

    #endregion

    #region TrainingCompletedCommand Compatibility Tests

    [Test]
    public void CommandEnvelope_TrainingCompletedCommand_DeserializesFromPersistedCamelCasePayload()
    {
        // Arrange - simulate persisted envelope with camelCase payload
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var persistedPayloadJson = $$"""{"userId":"{{userId}}","trainingId":"{{trainingId}}"}""";
        var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand";

        // Act - resolve type and deserialize payload
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
        var deserializedCommand = JsonSerializer.Deserialize(persistedPayloadJson, resolvedType, SharedSerializationOptions.Current);

        // Assert - exact type and all payload fields preserved
        Assert.That(resolvedType, Is.EqualTo(typeof(TrainingCompletedCommand)));
        Assert.That(deserializedCommand, Is.Not.Null);
        
        var command = (TrainingCompletedCommand)deserializedCommand!;
        Assert.That(command.UserId, Is.EqualTo(userId));
        Assert.That(command.TrainingId, Is.EqualTo(trainingId));
    }

    [Test]
    public void CommandEnvelope_TrainingCompletedCommand_DeserializesFromPersistedPascalCasePayload()
    {
        // Arrange - simulate legacy persisted envelope with PascalCase payload
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var legacyPayloadJson = $$"""{"UserId":"{{userId}}","TrainingId":"{{trainingId}}"}""";
        var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand";

        // Act - resolve type and deserialize legacy payload
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
        var deserializedCommand = JsonSerializer.Deserialize(legacyPayloadJson, resolvedType, SharedSerializationOptions.Current);

        // Assert - PropertyNameCaseInsensitive enables legacy payload compatibility
        Assert.That(resolvedType, Is.EqualTo(typeof(TrainingCompletedCommand)));
        Assert.That(deserializedCommand, Is.Not.Null);
        
        var command = (TrainingCompletedCommand)deserializedCommand!;
        Assert.That(command.UserId, Is.EqualTo(userId));
        Assert.That(command.TrainingId, Is.EqualTo(trainingId));
    }

    [Test]
    public void CommandEnvelope_TrainingCompletedCommand_RoundTripPreservesAllFields()
    {
        // Arrange - create command with multiple properties
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var command = new TrainingCompletedCommand { UserId = userId, TrainingId = trainingId };
        var commandType = typeof(TrainingCompletedCommand);

        // Act - serialize and deserialize
        var payloadJson = JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);
        var deserializedCommand = JsonSerializer.Deserialize(payloadJson, resolvedType, SharedSerializationOptions.Current);

        // Assert - all fields preserved through roundtrip
        Assert.That(resolvedType, Is.EqualTo(commandType));
        var roundTrippedCommand = (TrainingCompletedCommand)deserializedCommand!;
        Assert.That(roundTrippedCommand.UserId, Is.EqualTo(userId));
        Assert.That(roundTrippedCommand.TrainingId, Is.EqualTo(trainingId));
        
        // Verify serialized payload uses camelCase
        Assert.That(payloadJson, Does.Contain("userId"));
        Assert.That(payloadJson, Does.Contain("trainingId"));
    }

    #endregion

    #region InvitationCreatedCommand Compatibility Tests

    [Test]
    public void CommandEnvelope_InvitationCreatedCommand_DeserializesFromPersistedCamelCasePayload()
    {
        // Arrange - simulate persisted envelope with camelCase payload
        var invitationId = Guid.NewGuid();
        var persistedPayloadJson = $$"""{"invitationId":"{{invitationId}}"}""";
        var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand";

        // Act - resolve type and deserialize payload
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
        var deserializedCommand = JsonSerializer.Deserialize(persistedPayloadJson, resolvedType, SharedSerializationOptions.Current);

        // Assert - exact type and payload field preservation
        Assert.That(resolvedType, Is.EqualTo(typeof(InvitationCreatedCommand)));
        Assert.That(deserializedCommand, Is.Not.Null);
        
        var command = (InvitationCreatedCommand)deserializedCommand!;
        Assert.That(command.InvitationId, Is.EqualTo(invitationId));
    }

    [Test]
    public void CommandEnvelope_InvitationCreatedCommand_DeserializesFromPersistedPascalCasePayload()
    {
        // Arrange - simulate legacy persisted envelope with PascalCase payload
        var invitationId = Guid.NewGuid();
        var legacyPayloadJson = $$"""{"InvitationId":"{{invitationId}}"}""";
        var persistedDiscriminator = "LgymApi.BackgroundWorker.Common.Commands.InvitationCreatedCommand";

        // Act - resolve type and deserialize legacy payload
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(persistedDiscriminator);
        var deserializedCommand = JsonSerializer.Deserialize(legacyPayloadJson, resolvedType, SharedSerializationOptions.Current);

        // Assert - PropertyNameCaseInsensitive enables legacy payload compatibility
        Assert.That(resolvedType, Is.EqualTo(typeof(InvitationCreatedCommand)));
        Assert.That(deserializedCommand, Is.Not.Null);
        
        var command = (InvitationCreatedCommand)deserializedCommand!;
        Assert.That(command.InvitationId, Is.EqualTo(invitationId));
    }

    [Test]
    public void CommandEnvelope_InvitationCreatedCommand_RoundTripPreservesPayloadAndDiscriminator()
    {
        // Arrange - create command
        var invitationId = Guid.NewGuid();
        var command = new InvitationCreatedCommand { InvitationId = invitationId };
        var commandType = typeof(InvitationCreatedCommand);

        // Act - serialize and deserialize
        var payloadJson = JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);
        var discriminator = CommandTypeDiscriminatorPolicy.GetDiscriminator(commandType);
        var resolvedType = CommandTypeDiscriminatorPolicy.ResolveType(discriminator);
        var deserializedCommand = JsonSerializer.Deserialize(payloadJson, resolvedType, SharedSerializationOptions.Current);

        // Assert - full roundtrip preserves type and payload
        Assert.That(resolvedType, Is.EqualTo(commandType));
        var roundTrippedCommand = (InvitationCreatedCommand)deserializedCommand!;
        Assert.That(roundTrippedCommand.InvitationId, Is.EqualTo(invitationId));
        
        // Verify serialized payload uses camelCase
        Assert.That(payloadJson, Does.Contain("invitationId"));
        Assert.That(payloadJson, Does.Not.Contain("InvitationId"));
    }

    #endregion

    #region Cross-Command Compatibility Tests

    [Test]
    public void CommandEnvelope_AllKnownCommands_DeserializeFromPersistedEnvelopes()
    {
        // Arrange - simulate persisted envelopes for all known commands
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

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

            Assert.That(resolvedType, Is.EqualTo(envelope.ExpectedType),
                $"Failed to resolve type for discriminator: {envelope.Discriminator}");
            Assert.That(deserializedCommand, Is.Not.Null,
                $"Failed to deserialize payload for discriminator: {envelope.Discriminator}");
            Assert.That(deserializedCommand, Is.InstanceOf(envelope.ExpectedType),
                $"Deserialized command is not of expected type: {envelope.ExpectedType}");
        }
    }

    [Test]
    public void CommandEnvelope_MixedCasePayloads_AllDeserializeSuccessfully()
    {
        // Arrange - simulate mixed legacy and current payloads
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();

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

            Assert.That(deserializedCommand, Is.Not.Null,
                $"Failed to deserialize mixed-case payload: {payload.PayloadJson}");
            Assert.That(deserializedCommand, Is.InstanceOf(payload.ExpectedType),
                $"Deserialized command is not of expected type: {payload.ExpectedType}");
        }
    }

    [Test]
    public void CommandEnvelope_FullLifecycleSimulation_PreservesPayloadIntegrity()
    {
        // Arrange - simulate full CommandEnvelope lifecycle for all known commands
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

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
                Id = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
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
            Assert.That(resolvedType, Is.EqualTo(commandType));
            Assert.That(deserializedCommand, Is.Not.Null);
            Assert.That(deserializedCommand, Is.InstanceOf(commandType));
            
            // Verify specific field values based on command type
            switch (deserializedCommand)
            {
                case UserRegisteredCommand urc:
                    Assert.That(urc.UserId, Is.EqualTo(userId));
                    break;
                case TrainingCompletedCommand tcc:
                    Assert.That(tcc.UserId, Is.EqualTo(userId));
                    Assert.That(tcc.TrainingId, Is.EqualTo(trainingId));
                    break;
                case InvitationCreatedCommand icc:
                    Assert.That(icc.InvitationId, Is.EqualTo(invitationId));
                    break;
            }
        }
    }

    #endregion
}
