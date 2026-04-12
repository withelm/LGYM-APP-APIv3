using LgymApi.Domain.ValueObjects;
using System.Text.Json;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Common.Serialization;
using LgymApi.Domain.Enums;
using NUnit.Framework;
using FluentAssertions;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests roundtrip serialization/deserialization compatibility for email payload contracts.
/// Verifies that:
/// 1. Email payloads serialize and deserialize symmetrically using SharedSerializationOptions
/// 2. Legacy JSON payloads (PascalCase, old field shapes) remain readable (backward compatibility)
/// 3. Nested data structures (TrainingExerciseSummary) survive roundtrip intact
/// 4. Infrastructure composers can consume serialized payload JSON without errors
/// </summary>
[TestFixture]
public sealed class EmailPayloadRoundtripCompatibilityTests
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

    #region WelcomeEmailPayload Tests

    [Test]
    public void WelcomeEmailPayload_RoundtripsSuccessfully()
    {
        // Arrange
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var originalPayload = new WelcomeEmailPayload
        {
            UserId = userId,
            UserName = "John Doe",
            RecipientEmail = "john@example.com",
            CultureName = "en-US"
        };
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<WelcomeEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(userId);
        deserialized.UserName.Should().Be("John Doe");
        deserialized.RecipientEmail.Should().Be("john@example.com");
        deserialized.CultureName.Should().Be("en-US");
    }

    [Test]
    public void WelcomeEmailPayload_SerializedJsonUsesCamelCase()
    {
        // Arrange
        var originalPayload = new WelcomeEmailPayload
        {
            UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New(),
            UserName = "Jane Doe",
            RecipientEmail = "jane@example.com",
            CultureName = "pl-PL"
        };

        // Act
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Assert
        json.Should().Contain("userId");
        json.Should().Contain("userName");
        json.Should().Contain("recipientEmail");
        json.Should().Contain("cultureName");
        json.Should().NotContain("UserId");
        json.Should().NotContain("UserName");
    }

    [Test]
    public void WelcomeEmailPayload_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property names
        const string legacyUserId = "a50e8400-e29b-41d4-a716-446655440001";
        var legacyJson = "{\"UserId\":\"" + legacyUserId + "\"," +
                         "\"UserName\":\"Legacy User\"," +
                         "\"RecipientEmail\":\"legacy@example.com\"," +
                         "\"CultureName\":\"de-DE\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<WelcomeEmailPayload>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(ParseTestId<LgymApi.Domain.Entities.User>(legacyUserId));
        deserialized.UserName.Should().Be("Legacy User");
        deserialized.RecipientEmail.Should().Be("legacy@example.com");
        deserialized.CultureName.Should().Be("de-DE");
    }

    [Test]
    public void WelcomeEmailPayload_RoundtripsPreservesAllFields()
    {
        // Arrange
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var originalPayload = new WelcomeEmailPayload
        {
            UserId = userId,
            UserName = "Test User",
            RecipientEmail = "test@example.com",
            CultureName = "es-ES"
        };
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<WelcomeEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert - each field must match exactly
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(userId, "UserId not preserved");
        deserialized.UserName.Should().Be("Test User", "UserName not preserved");
        deserialized.RecipientEmail.Should().Be("test@example.com", "RecipientEmail not preserved");
        deserialized.CultureName.Should().Be("es-ES", "CultureName not preserved");
    }

    [Test]
    public void WelcomeEmailPayload_CorrelationIdDerivesFromUserId()
    {
        // Arrange
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var payload = new WelcomeEmailPayload
        {
            UserId = userId,
            UserName = "Corr User",
            RecipientEmail = "corr@example.com",
            CultureName = "fr-FR"
        };

        // Act
        var json = JsonSerializer.Serialize(payload, SharedSerializationOptions.Current);
        var deserialized = JsonSerializer.Deserialize<WelcomeEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.GetValue().Should().Be(userId.GetValue());
    }

    #endregion

    #region InvitationEmailPayload Tests

    [Test]
    public void InvitationEmailPayload_RoundtripsSuccessfully()
    {
        // Arrange
        var invitationId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>.New();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var originalPayload = new InvitationEmailPayload
        {
            InvitationId = invitationId,
            InvitationCode = "INV12345",
            ExpiresAt = expiresAt,
            TrainerName = "Coach Mike",
            RecipientEmail = "client@example.com",
            CultureName = "en-US",
            PreferredTimeZone = "America/New_York"
        };
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<InvitationEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.InvitationId.Should().Be(invitationId);
        deserialized.InvitationCode.Should().Be("INV12345");
        deserialized.ExpiresAt.Should().Be(expiresAt);
        deserialized.TrainerName.Should().Be("Coach Mike");
        deserialized.RecipientEmail.Should().Be("client@example.com");
        deserialized.CultureName.Should().Be("en-US");
        deserialized.PreferredTimeZone.Should().Be("America/New_York");
    }

    [Test]
    public void InvitationEmailPayload_SerializedJsonUsesCamelCase()
    {
        // Arrange
        var originalPayload = new InvitationEmailPayload
        {
            InvitationId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>.New(),
            InvitationCode = "TEST123",
            ExpiresAt = DateTimeOffset.UtcNow,
            TrainerName = "Trainer",
            RecipientEmail = "recipient@example.com",
            CultureName = "pl-PL",
            PreferredTimeZone = "Europe/Warsaw"
        };

        // Act
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Assert
        json.Should().Contain("invitationId");
        json.Should().Contain("invitationCode");
        json.Should().Contain("expiresAt");
        json.Should().Contain("trainerName");
        json.Should().Contain("recipientEmail");
        json.Should().Contain("cultureName");
        json.Should().Contain("preferredTimeZone");
        json.Should().NotContain("InvitationId");
        json.Should().NotContain("ExpiresAt");
    }

    [Test]
    public void InvitationEmailPayload_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property names
        const string legacyInvitationId = "b50e8400-e29b-41d4-a716-446655440002";
        var expiresAt = DateTimeOffset.UtcNow.AddDays(14);
        var legacyJson = "{\"InvitationId\":\"" + legacyInvitationId + "\"," +
                         "\"InvitationCode\":\"LEGACY123\"," +
                         "\"ExpiresAt\":\"" + expiresAt.ToString("O") + "\"," +
                         "\"TrainerName\":\"Old Trainer\"," +
                         "\"RecipientEmail\":\"oldclient@example.com\"," +
                         "\"CultureName\":\"de-DE\"," +
                         "\"PreferredTimeZone\":\"Europe/Berlin\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<InvitationEmailPayload>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.InvitationId.Should().Be(ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>(legacyInvitationId));
        deserialized.InvitationCode.Should().Be("LEGACY123");
        deserialized.TrainerName.Should().Be("Old Trainer");
        deserialized.RecipientEmail.Should().Be("oldclient@example.com");
    }

    [Test]
    public void InvitationEmailPayload_RoundtripsPreservesAllFields()
    {
        // Arrange
        var invitationId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>.New();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        var originalPayload = new InvitationEmailPayload
        {
            InvitationId = invitationId,
            InvitationCode = "FULL123",
            ExpiresAt = expiresAt,
            TrainerName = "Full Trainer",
            RecipientEmail = "fullclient@example.com",
            CultureName = "fr-FR",
            PreferredTimeZone = "Europe/Paris"
        };
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<InvitationEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert - each field must match exactly
        deserialized.Should().NotBeNull();
        deserialized!.InvitationId.Should().Be(invitationId, "InvitationId not preserved");
        deserialized.InvitationCode.Should().Be("FULL123", "InvitationCode not preserved");
        deserialized.ExpiresAt.Should().Be(expiresAt, "ExpiresAt not preserved");
        deserialized.TrainerName.Should().Be("Full Trainer", "TrainerName not preserved");
        deserialized.RecipientEmail.Should().Be("fullclient@example.com", "RecipientEmail not preserved");
        deserialized.CultureName.Should().Be("fr-FR", "CultureName not preserved");
        deserialized.PreferredTimeZone.Should().Be("Europe/Paris", "PreferredTimeZone not preserved");
    }

    [Test]
    public void InvitationEmailPayload_CorrelationIdDerivesFromInvitationId()
    {
        // Arrange
        var invitationId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>.New();
        var payload = new InvitationEmailPayload
        {
            InvitationId = invitationId,
            InvitationCode = "CORR123",
            ExpiresAt = DateTimeOffset.UtcNow,
            TrainerName = "Corr Trainer",
            RecipientEmail = "corrclient@example.com",
            CultureName = "en-GB",
            PreferredTimeZone = "Europe/London"
        };

        // Act
        var json = JsonSerializer.Serialize(payload, SharedSerializationOptions.Current);
        var deserialized = JsonSerializer.Deserialize<InvitationEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.GetValue().Should().Be(invitationId.GetValue());
    }

    #endregion

    #region TrainingCompletedEmailPayload Tests

    [Test]
    public void TrainingCompletedEmailPayload_RoundtripsSuccessfully()
    {
        // Arrange
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var trainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New();
        var trainingDate = DateTimeOffset.UtcNow;
        var originalPayload = new TrainingCompletedEmailPayload
        {
            UserId = userId,
            TrainingId = trainingId,
            RecipientEmail = "athlete@example.com",
            CultureName = "en-US",
            PreferredTimeZone = "America/Los_Angeles",
            PlanDayName = "Upper Body A",
            TrainingDate = trainingDate,
            Exercises = new List<TrainingExerciseSummary>
            {
                new()
                {
                    ExerciseId = "ex1",
                    ExerciseName = "Bench Press",
                    Series = 1,
                    Reps = 8,
                    Weight = 80,
                    Unit = WeightUnits.Kilograms
                }
            }
        };
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(userId);
        deserialized.TrainingId.Should().Be(trainingId);
        deserialized.RecipientEmail.Should().Be("athlete@example.com");
        deserialized.CultureName.Should().Be("en-US");
        deserialized.PreferredTimeZone.Should().Be("America/Los_Angeles");
        deserialized.PlanDayName.Should().Be("Upper Body A");
        deserialized.TrainingDate.Should().Be(trainingDate);
    }

    [Test]
    public void TrainingCompletedEmailPayload_NestedExercisesSurviveRoundtrip()
    {
        // Arrange
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var trainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New();
        var originalPayload = new TrainingCompletedEmailPayload
        {
            UserId = userId,
            TrainingId = trainingId,
            RecipientEmail = "athlete@example.com",
            CultureName = "en-US",
            PreferredTimeZone = "America/New_York",
            PlanDayName = "Full Body",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new List<TrainingExerciseSummary>
            {
                new()
                {
                    ExerciseId = "ex1",
                    ExerciseName = "Bench Press",
                    Series = 1,
                    Reps = 8,
                    Weight = 80,
                    Unit = WeightUnits.Kilograms
                },
                new()
                {
                    ExerciseId = "ex2",
                    ExerciseName = "Squat",
                    Series = 2,
                    Reps = 6,
                    Weight = 120,
                    Unit = WeightUnits.Kilograms
                }
            }
        };
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Exercises.Should().NotBeNull();
        deserialized.Exercises.Count.Should().Be(2);

         // Verify first exercise
         var firstExercise = deserialized.Exercises.First();
         firstExercise.ExerciseId.Should().Be("ex1");
         firstExercise.ExerciseName.Should().Be("Bench Press");
         firstExercise.Series.Should().Be(1);
         firstExercise.Reps.Should().Be(8);
         firstExercise.Weight.Should().Be(80);
         firstExercise.Unit.Should().Be(WeightUnits.Kilograms);

         // Verify second exercise
         var secondExercise = deserialized.Exercises.Skip(1).First();
         secondExercise.ExerciseId.Should().Be("ex2");
         secondExercise.ExerciseName.Should().Be("Squat");
         secondExercise.Series.Should().Be(2);
         secondExercise.Reps.Should().Be(6);
         secondExercise.Weight.Should().Be(120);
         secondExercise.Unit.Should().Be(WeightUnits.Kilograms);
    }

    [Test]
    public void TrainingCompletedEmailPayload_SerializedJsonUsesCamelCase()
    {
        // Arrange
        var originalPayload = new TrainingCompletedEmailPayload
        {
            UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New(),
            TrainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New(),
            RecipientEmail = "test@example.com",
            CultureName = "pl-PL",
            PreferredTimeZone = "Europe/Warsaw",
            PlanDayName = "Test Day",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new List<TrainingExerciseSummary>()
        };

        // Act
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Assert
        json.Should().Contain("userId");
        json.Should().Contain("trainingId");
        json.Should().Contain("recipientEmail");
        json.Should().Contain("cultureName");
        json.Should().Contain("preferredTimeZone");
        json.Should().Contain("planDayName");
        json.Should().Contain("trainingDate");
        json.Should().Contain("exercises");
        json.Should().NotContain("UserId");
        json.Should().NotContain("Exercises");
    }

    [Test]
    public void TrainingCompletedEmailPayload_NestedExercisesUseCamelCase()
    {
        // Arrange
        var payload = new TrainingCompletedEmailPayload
        {
            UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New(),
            TrainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New(),
            RecipientEmail = "test@example.com",
            CultureName = "en-US",
            PreferredTimeZone = "UTC",
            PlanDayName = "Test",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new List<TrainingExerciseSummary>
            {
                new()
                {
                    ExerciseId = "ex1",
                    ExerciseName = "Test Exercise",
                    Series = 1,
                    Reps = 10,
                    Weight = 50,
                    Unit = WeightUnits.Pounds
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(payload, SharedSerializationOptions.Current);

        // Assert - verify nested properties use camelCase
        json.Should().Contain("exerciseId");
        json.Should().Contain("exerciseName");
        json.Should().Contain("series");
        json.Should().Contain("reps");
        json.Should().Contain("weight");
        json.Should().Contain("unit");
        json.Should().NotContain("ExerciseId");
        json.Should().NotContain("ExerciseName");
    }

    [Test]
    public void TrainingCompletedEmailPayload_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property names
        const string legacyUserId = "c50e8400-e29b-41d4-a716-446655440003";
        const string legacyTrainingId = "c50e8400-e29b-41d4-a716-446655440004";
        var trainingDate = DateTimeOffset.UtcNow;
        var legacyJson = "{\"UserId\":\"" + legacyUserId + "\"," +
                         "\"TrainingId\":\"" + legacyTrainingId + "\"," +
                         "\"RecipientEmail\":\"legacy@example.com\"," +
                         "\"CultureName\":\"de-DE\"," +
                         "\"PreferredTimeZone\":\"Europe/Berlin\"," +
                         "\"PlanDayName\":\"Legacy Day\"," +
                         "\"TrainingDate\":\"" + trainingDate.ToString("O") + "\"," +
                         "\"Exercises\":[{\"ExerciseId\":\"ex1\"," +
                         "\"ExerciseName\":\"Legacy Exercise\"," +
                         "\"Series\":1," +
                         "\"Reps\":5," +
                         "\"Weight\":45," +
                         "\"Unit\":\"Kilograms\"}]}";

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(ParseTestId<LgymApi.Domain.Entities.User>(legacyUserId));
        deserialized.TrainingId.Should().Be(ParseTestId<LgymApi.Domain.Entities.Training>(legacyTrainingId));
        deserialized.RecipientEmail.Should().Be("legacy@example.com");
        deserialized.Exercises.Count.Should().Be(1);
        deserialized.Exercises.First().ExerciseName.Should().Be("Legacy Exercise");
    }

    [Test]
    public void TrainingCompletedEmailPayload_DeserializesFromLegacyIntegerEnumValues()
    {
        // Arrange - simulate very old persisted payload with integer enum values (numeric 1 for Kilograms)
        // This represents payloads serialized before string enum enforcement
        const string legacyUserId = "c50e8400-e29b-41d4-a716-446655440005";
        const string legacyTrainingId = "c50e8400-e29b-41d4-a716-446655440006";
        var trainingDate = DateTimeOffset.UtcNow;
        var legacyJson = "{\"UserId\":\"" + legacyUserId + "\"," +
                         "\"TrainingId\":\"" + legacyTrainingId + "\"," +
                         "\"RecipientEmail\":\"ancient@example.com\"," +
                         "\"CultureName\":\"en-US\"," +
                         "\"PreferredTimeZone\":\"UTC\"," +
                         "\"PlanDayName\":\"Ancient Day\"," +
                         "\"TrainingDate\":\"" + trainingDate.ToString("O") + "\"," +
                         "\"Exercises\":[{\"ExerciseId\":\"ex1\"," +
                         "\"ExerciseName\":\"Ancient Exercise\"," +
                         "\"Series\":1," +
                         "\"Reps\":8," +
                         "\"Weight\":50," +
                         "\"Unit\":1}]}";  // 1 = Kilograms enum value

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(ParseTestId<LgymApi.Domain.Entities.User>(legacyUserId));
        deserialized.TrainingId.Should().Be(ParseTestId<LgymApi.Domain.Entities.Training>(legacyTrainingId));
        deserialized.RecipientEmail.Should().Be("ancient@example.com");
        deserialized.Exercises.Count.Should().Be(1);
        deserialized.Exercises.First().ExerciseName.Should().Be("Ancient Exercise");
        // Verify the enum was properly deserialized from integer value
        deserialized.Exercises.First().Unit.Should().Be(WeightUnits.Kilograms);
    }

    [Test]
    public void TrainingCompletedEmailPayload_RoundtripsPreservesAllFields()
    {
        // Arrange
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var trainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New();
        var trainingDate = DateTimeOffset.UtcNow;
        var originalPayload = new TrainingCompletedEmailPayload
        {
            UserId = userId,
            TrainingId = trainingId,
            RecipientEmail = "full@example.com",
            CultureName = "es-ES",
            PreferredTimeZone = "Europe/Madrid",
            PlanDayName = "Full Upper",
            TrainingDate = trainingDate,
            Exercises = new List<TrainingExerciseSummary>
            {
                new()
                {
                    ExerciseId = "ex1",
                    ExerciseName = "Full Bench",
                    Series = 3,
                    Reps = 8,
                    Weight = 100,
                    Unit = WeightUnits.Kilograms
                }
            }
        };
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert - each field must match exactly
        deserialized.Should().NotBeNull();
        deserialized!.UserId.Should().Be(userId, "UserId not preserved");
        deserialized.TrainingId.Should().Be(trainingId, "TrainingId not preserved");
        deserialized.RecipientEmail.Should().Be("full@example.com", "RecipientEmail not preserved");
        deserialized.CultureName.Should().Be("es-ES", "CultureName not preserved");
        deserialized.PreferredTimeZone.Should().Be("Europe/Madrid", "PreferredTimeZone not preserved");
        deserialized.PlanDayName.Should().Be("Full Upper", "PlanDayName not preserved");
        deserialized.TrainingDate.Should().Be(trainingDate, "TrainingDate not preserved");
        deserialized.Exercises.Count.Should().Be(1, "Exercises count not preserved");
    }

    [Test]
    public void TrainingCompletedEmailPayload_CorrelationIdDerivesFromTrainingId()
    {
        // Arrange
        var trainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New();
        var payload = new TrainingCompletedEmailPayload
        {
            UserId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New(),
            TrainingId = trainingId,
            RecipientEmail = "corr@example.com",
            CultureName = "en-US",
            PreferredTimeZone = "UTC",
            PlanDayName = "Corr Day",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new List<TrainingExerciseSummary>()
        };

        // Act
        var json = JsonSerializer.Serialize(payload, SharedSerializationOptions.Current);
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.GetValue().Should().Be(trainingId.GetValue());
    }

    #endregion

    #region Composer Compatibility Tests

    [Test]
    public void WelcomeEmailPayload_CanBeDeserializedByComposerConsumer()
    {
        // Arrange - simulate payload stored by EmailSchedulerService and read back by composer
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var storedPayload = new WelcomeEmailPayload
        {
            UserId = userId,
            UserName = "Composer Test",
            RecipientEmail = "composer@example.com",
            CultureName = "en-US"
        };
        // Simulate what EmailSchedulerService would persist
        var storedJson = JsonSerializer.Serialize(storedPayload, SharedSerializationOptions.Current);

        // Act - simulate what a composer would do
        var deserializedPayload = JsonSerializer.Deserialize<WelcomeEmailPayload>(storedJson, SharedSerializationOptions.Current);

        // Assert - composer can consume the payload without error
        deserializedPayload.Should().NotBeNull();
        deserializedPayload!.UserName.Should().Be("Composer Test");
        deserializedPayload.RecipientEmail.Should().Be("composer@example.com");
    }

    [Test]
    public void InvitationEmailPayload_CanBeDeserializedByComposerConsumer()
    {
        // Arrange - simulate payload stored by EmailSchedulerService and read back by composer
        var invitationId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>.New();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var storedPayload = new InvitationEmailPayload
        {
            InvitationId = invitationId,
            InvitationCode = "COMP123",
            ExpiresAt = expiresAt,
            TrainerName = "Composer Trainer",
            RecipientEmail = "compinv@example.com",
            CultureName = "pl-PL",
            PreferredTimeZone = "Europe/Warsaw"
        };
        // Simulate what EmailSchedulerService would persist
        var storedJson = JsonSerializer.Serialize(storedPayload, SharedSerializationOptions.Current);

        // Act - simulate what a composer would do
        var deserializedPayload = JsonSerializer.Deserialize<InvitationEmailPayload>(storedJson, SharedSerializationOptions.Current);

        // Assert - composer can consume the payload without error
        deserializedPayload.Should().NotBeNull();
        deserializedPayload!.InvitationCode.Should().Be("COMP123");
        deserializedPayload.TrainerName.Should().Be("Composer Trainer");
        deserializedPayload.ExpiresAt.Should().Be(expiresAt);
    }

    [Test]
    public void TrainingCompletedEmailPayload_CanBeDeserializedByComposerConsumer()
    {
        // Arrange - simulate payload stored by EmailSchedulerService and read back by composer
        var userId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>.New();
        var trainingId = LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>.New();
        var trainingDate = DateTimeOffset.UtcNow;
        var storedPayload = new TrainingCompletedEmailPayload
        {
            UserId = userId,
            TrainingId = trainingId,
            RecipientEmail = "comptraining@example.com",
            CultureName = "en-US",
            PreferredTimeZone = "America/New_York",
            PlanDayName = "Composer Training",
            TrainingDate = trainingDate,
            Exercises = new List<TrainingExerciseSummary>
            {
                new()
                {
                    ExerciseId = "comp1",
                    ExerciseName = "Composer Exercise",
                    Series = 1,
                    Reps = 8,
                    Weight = 75,
                    Unit = WeightUnits.Kilograms
                }
            }
        };
        // Simulate what EmailSchedulerService would persist
        var storedJson = JsonSerializer.Serialize(storedPayload, SharedSerializationOptions.Current);

        // Act - simulate what a composer would do
        var deserializedPayload = JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(storedJson, SharedSerializationOptions.Current);

        // Assert - composer can consume the payload without error, including nested exercises
        deserializedPayload.Should().NotBeNull();
        deserializedPayload!.PlanDayName.Should().Be("Composer Training");
        deserializedPayload.Exercises.Count.Should().Be(1);
        deserializedPayload.Exercises.First().ExerciseName.Should().Be("Composer Exercise");
    }

    #endregion
}



