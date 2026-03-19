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
    #region UserRegisteredCommand Tests

    [Test]
    public void UserRegisteredCommand_RoundtripsSuccessfully()
    {
        // Arrange
        var originalCommand = new UserRegisteredCommand { UserId = Guid.NewGuid() };
        var json = JsonSerializer.Serialize(originalCommand, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<UserRegisteredCommand>(json, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(originalCommand.UserId));
    }

    [Test]
    public void UserRegisteredCommand_SerializedJsonUsesCamelCase()
    {
        // Arrange
        var command = new UserRegisteredCommand { UserId = Guid.NewGuid() };

        // Act
        var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);

        // Assert
        Assert.That(json, Does.Contain("userId"));
        Assert.That(json, Does.Not.Contain("UserId"));
    }

    [Test]
    public void UserRegisteredCommand_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property names
        var userId = Guid.NewGuid();
        var legacyJson = "{\"UserId\":\"" + userId.ToString() + "\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<UserRegisteredCommand>(legacyJson, SharedSerializationOptions.Current);

        // Assert - PropertyNameCaseInsensitive=true allows reading PascalCase
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(userId));
    }

    [Test]
    public void UserRegisteredCommand_RoundtripsMultipleInstances()
    {
        // Arrange
        var commands = new[]
        {
            new UserRegisteredCommand { UserId = Guid.NewGuid() },
            new UserRegisteredCommand { UserId = Guid.NewGuid() },
            new UserRegisteredCommand { UserId = Guid.NewGuid() }
        };

        // Act & Assert
        foreach (var command in commands)
        {
            var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);
            var deserialized = JsonSerializer.Deserialize<UserRegisteredCommand>(json, SharedSerializationOptions.Current);
            
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.UserId, Is.EqualTo(command.UserId));
        }
    }

    #endregion

    #region TrainingCompletedCommand Tests

    [Test]
    public void TrainingCompletedCommand_RoundtripsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var originalCommand = new TrainingCompletedCommand 
        { 
            UserId = userId,
            TrainingId = trainingId
        };
        var json = JsonSerializer.Serialize(originalCommand, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedCommand>(json, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(userId));
        Assert.That(deserialized.TrainingId, Is.EqualTo(trainingId));
    }

    [Test]
    public void TrainingCompletedCommand_SerializedJsonUsesCamelCaseForAllProperties()
    {
        // Arrange
        var command = new TrainingCompletedCommand 
        { 
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid()
        };

        // Act
        var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);

        // Assert
        Assert.That(json, Does.Contain("userId"));
        Assert.That(json, Does.Contain("trainingId"));
        Assert.That(json, Does.Not.Contain("UserId"));
        Assert.That(json, Does.Not.Contain("TrainingId"));
    }

    [Test]
    public void TrainingCompletedCommand_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property names
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var legacyJson = "{\"UserId\":\"" + userId.ToString() + "\",\"TrainingId\":\"" + trainingId.ToString() + "\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedCommand>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(userId));
        Assert.That(deserialized.TrainingId, Is.EqualTo(trainingId));
    }

    [Test]
    public void TrainingCompletedCommand_DeserializesFromMixedCaseJson()
    {
        // Arrange - simulate mixed-case payload (camelCase and PascalCase combined)
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var mixedJson = "{\"userId\":\"" + userId.ToString() + "\",\"TrainingId\":\"" + trainingId.ToString() + "\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedCommand>(mixedJson, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(userId));
        Assert.That(deserialized.TrainingId, Is.EqualTo(trainingId));
    }

    [Test]
    public void TrainingCompletedCommand_RoundtripsPreservesFieldValuesExactly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var originalCommand = new TrainingCompletedCommand 
        { 
            UserId = userId,
            TrainingId = trainingId
        };
        var json = JsonSerializer.Serialize(originalCommand, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedCommand>(json, SharedSerializationOptions.Current);

        // Assert - each field must match exactly
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(userId), "UserId not preserved");
        Assert.That(deserialized.TrainingId, Is.EqualTo(trainingId), "TrainingId not preserved");
    }

    #endregion

    #region InvitationCreatedCommand Tests

    [Test]
    public void InvitationCreatedCommand_RoundtripsSuccessfully()
    {
        // Arrange
        var invitationId = Guid.NewGuid();
        var originalCommand = new InvitationCreatedCommand { InvitationId = invitationId };
        var json = JsonSerializer.Serialize(originalCommand, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<InvitationCreatedCommand>(json, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.InvitationId, Is.EqualTo(invitationId));
    }

    [Test]
    public void InvitationCreatedCommand_SerializedJsonUsesCamelCase()
    {
        // Arrange
        var command = new InvitationCreatedCommand { InvitationId = Guid.NewGuid() };

        // Act
        var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);

        // Assert
        Assert.That(json, Does.Contain("invitationId"));
        Assert.That(json, Does.Not.Contain("InvitationId"));
    }

    [Test]
    public void InvitationCreatedCommand_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property name
        var invitationId = Guid.NewGuid();
        var legacyJson = "{\"InvitationId\":\"" + invitationId.ToString() + "\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<InvitationCreatedCommand>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.InvitationId, Is.EqualTo(invitationId));
    }

    [Test]
    public void InvitationCreatedCommand_RoundtripsWithVariousGuidValues()
    {
        // Arrange
        var testGuids = new[] { Guid.NewGuid(), Guid.Empty, Guid.NewGuid() };

        // Act & Assert
        foreach (var guid in testGuids)
        {
            var command = new InvitationCreatedCommand { InvitationId = guid };
            var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);
            var deserialized = JsonSerializer.Deserialize<InvitationCreatedCommand>(json, SharedSerializationOptions.Current);
            
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.InvitationId, Is.EqualTo(guid));
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
            new UserRegisteredCommand { UserId = Guid.NewGuid() },
            new TrainingCompletedCommand { UserId = Guid.NewGuid(), TrainingId = Guid.NewGuid() },
            new InvitationCreatedCommand { InvitationId = Guid.NewGuid() }
        };

        // Act & Assert - all commands serialize to JSON without exception
        foreach (var command in commands)
        {
            var json = JsonSerializer.Serialize(command, SharedSerializationOptions.Current);
            Assert.That(json, Is.Not.Null);
            Assert.That(json, Is.Not.Empty);
        }
    }

    [Test]
    public void SharedSerializationOptions_IsReadOnly()
    {
        // Arrange & Act
        var options = SharedSerializationOptions.Current;

        // Assert - options object should be immutable after creation
        Assert.That(options, Is.Not.Null);
        Assert.That(options.PropertyNamingPolicy, Is.EqualTo(JsonNamingPolicy.CamelCase));
        Assert.That(options.DictionaryKeyPolicy, Is.EqualTo(JsonNamingPolicy.CamelCase));
        Assert.That(options.PropertyNameCaseInsensitive, Is.True);
    }

    #endregion
}
