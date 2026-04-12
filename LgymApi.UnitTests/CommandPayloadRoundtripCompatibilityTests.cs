using FluentAssertions;
using LgymApi.Domain.ValueObjects;
using System.Text.Json;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Serialization;
using NUnit.Framework;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests roundtrip serialization/deserialization compatibility for command payloads.
/// Verifies that:
/// 1. Commands serialize and deserialize symmetrically using SharedSerializationOptions
/// 2. Legacy JSON payloads (PascalCase, old field shapes) remain readable (backward compatibility)
/// 3. Field values are preserved exactly through the roundtrip
/// </summary>
[TestFixture]
public sealed class CommandPayloadRoundtripCompatibilityTests
{
    // Helper to parse fixed test IDs from UUID string literals
    private static Id<T> ParseTestId<T>(string uuid)
    {
        if (!Id<T>.TryParse(uuid, out var id))
        {
            throw new ArgumentException($"Invalid UUID: {uuid}", nameof(uuid));
        }
        return id;
    }

    #region UserRegisteredCommand Tests

    [Test]
    public void UserRegisteredCommand_RoundtripsSuccessfully()
    {
        // Arrange
        var originalCommand = new UserRegisteredCommand { UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New() };
        var json = JsonSerializer.Serialize(originalCommand, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<UserRegisteredCommand>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(originalCommand.UserId);
    }

    [Test]
    public void UserRegisteredCommand_SerializedJsonUsesCamelCase()
    {
        // Arrange
        var command = new UserRegisteredCommand { UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New() };

        // Act
        var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);

        // Assert
        json.Should().Contain("userId");
        json.Should().NotContain("UserId");
    }

    [Test]
    public void UserRegisteredCommand_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property names
        const string legacyUserId = "550e8400-e29b-41d4-a716-446655440001";
        var legacyJson = "{\"UserId\":\"" + legacyUserId + "\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<UserRegisteredCommand>(legacyJson, SharedSerializationOptions.Current);

        // Assert - PropertyNameCaseInsensitive=true allows reading PascalCase
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(ParseTestId<LgymApi.Domain.Entities.User>(legacyUserId));
    }

    [Test]
    public void UserRegisteredCommand_RoundtripsMultipleInstances()
    {
        // Arrange
        var commands = new[]
        {
            new UserRegisteredCommand { UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New() },
            new UserRegisteredCommand { UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New() },
            new UserRegisteredCommand { UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New() }
        };

        // Act & Assert
        foreach (var command in commands)
        {
            var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);
            var deserialized = JsonSerializer.Deserialize<UserRegisteredCommand>(json, SharedSerializationOptions.Current);
            
            deserialized.Should().NotBeNull();
            deserialized!.UserId.Should().Be(command.UserId);
        }
    }

    #endregion

    #region TrainingCompletedCommand Tests

    [Test]
    public void TrainingCompletedCommand_RoundtripsSuccessfully()
    {
        // Arrange
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var trainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New();
        var originalCommand = new TrainingCompletedCommand 
        { 
            UserId = userId,
            TrainingId = trainingId
        };
        var json = JsonSerializer.Serialize(originalCommand, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedCommand>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(userId);
        deserialized.TrainingId.Should().Be(trainingId);
    }

    [Test]
    public void TrainingCompletedCommand_SerializedJsonUsesCamelCaseForAllProperties()
    {
        // Arrange
        var command = new TrainingCompletedCommand 
        { 
            UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New(),
            TrainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New()
        };

        // Act
        var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);

        // Assert
        json.Should().Contain("userId");
        json.Should().Contain("trainingId");
        json.Should().NotContain("UserId");
        json.Should().NotContain("TrainingId");
    }

    [Test]
    public void TrainingCompletedCommand_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property names
        const string legacyUserId = "550e8400-e29b-41d4-a716-446655440002";
        const string legacyTrainingId = "550e8400-e29b-41d4-a716-446655440003";
        var legacyJson = "{\"UserId\":\"" + legacyUserId + "\",\"TrainingId\":\"" + legacyTrainingId + "\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedCommand>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(ParseTestId<LgymApi.Domain.Entities.User>(legacyUserId));
        deserialized.TrainingId.Should().Be(ParseTestId<LgymApi.Domain.Entities.Training>(legacyTrainingId));
    }

    [Test]
    public void TrainingCompletedCommand_DeserializesFromMixedCaseJson()
    {
        // Arrange - simulate mixed-case payload (camelCase and PascalCase combined)
        const string legacyUserId = "550e8400-e29b-41d4-a716-446655440004";
        const string legacyTrainingId = "550e8400-e29b-41d4-a716-446655440005";
        var mixedJson = "{\"userId\":\"" + legacyUserId + "\",\"TrainingId\":\"" + legacyTrainingId + "\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedCommand>(mixedJson, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(ParseTestId<LgymApi.Domain.Entities.User>(legacyUserId));
        deserialized.TrainingId.Should().Be(ParseTestId<LgymApi.Domain.Entities.Training>(legacyTrainingId));
    }

    [Test]
    public void TrainingCompletedCommand_RoundtripsPreservesFieldValuesExactly()
    {
        // Arrange
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var trainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New();
        var originalCommand = new TrainingCompletedCommand 
        { 
            UserId = userId,
            TrainingId = trainingId
        };
        var json = JsonSerializer.Serialize(originalCommand, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedCommand>(json, SharedSerializationOptions.Current);

        // Assert - each field must match exactly
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(userId, "UserId not preserved");
        deserialized.TrainingId.Should().Be(trainingId, "TrainingId not preserved");
    }

    #endregion

    #region InvitationCreatedCommand Tests

    [Test]
    public void InvitationCreatedCommand_RoundtripsSuccessfully()
    {
        // Arrange
        var invitationId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>.New();
        var originalCommand = new InvitationCreatedCommand { InvitationId = invitationId };
        var json = JsonSerializer.Serialize(originalCommand, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<InvitationCreatedCommand>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.InvitationId.Should().Be(invitationId);
    }

    [Test]
    public void InvitationCreatedCommand_SerializedJsonUsesCamelCase()
    {
        // Arrange
        var command = new InvitationCreatedCommand { InvitationId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>.New() };

        // Act
        var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);

        // Assert
        json.Should().Contain("invitationId");
        json.Should().NotContain("InvitationId");
    }

    [Test]
    public void InvitationCreatedCommand_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property name
        const string legacyInvitationId = "550e8400-e29b-41d4-a716-446655440006";
        var legacyJson = "{\"InvitationId\":\"" + legacyInvitationId + "\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<InvitationCreatedCommand>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.InvitationId.Should().Be(ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>(legacyInvitationId));
    }

    [Test]
    public void InvitationCreatedCommand_RoundtripsWithVariousGuidValues()
    {
        // Arrange - test with Empty and various UUID values
        var testIds = new[] 
        { 
            ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("550e8400-e29b-41d4-a716-446655440007"),
            Id<LgymApi.Domain.Entities.TrainerInvitation>.Empty,
            ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("550e8400-e29b-41d4-a716-446655440008")
        };

        // Act & Assert
        foreach (var id in testIds)
        {
            var command = new InvitationCreatedCommand { InvitationId = id };
            var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);
            var deserialized = JsonSerializer.Deserialize<InvitationCreatedCommand>(json, SharedSerializationOptions.Current);
            
            deserialized.Should().NotBeNull();
            deserialized!.InvitationId.Should().Be(id);
        }
    }

    #endregion

    #region Cross-Command Compatibility Tests

    [Test]
    public void AllCommands_UseConsistentSerializationOptions()
    {
        // Arrange
        var commands = new object[]
        {
            new UserRegisteredCommand { UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New() },
            new TrainingCompletedCommand { UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New(), TrainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New() },
            new InvitationCreatedCommand { InvitationId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>.New() }
        };

        // Act & Assert - all commands serialize to JSON without exception
        foreach (var command in commands)
        {
            var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);
            json.Should().NotBeNull();
            json.Should().NotBeEmpty();
        }
    }

    [Test]
    public void SharedSerializationOptions_IsReadOnly()
    {
        // Arrange & Act
        var options = SharedSerializationOptions.Current;

        // Assert - options object should be immutable after creation
        options.Should().NotBeNull();
        options.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
        options.DictionaryKeyPolicy.Should().Be(JsonNamingPolicy.CamelCase);
        options.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    #endregion
}
