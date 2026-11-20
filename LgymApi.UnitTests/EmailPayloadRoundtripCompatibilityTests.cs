using LgymApi.Domain.ValueObjects;
using System.Text.Json;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Enums;
using NUnit.Framework;

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
    private const string MalformedCultureName = "!invalid-culture-380";

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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(userId));
        Assert.That(deserialized.UserName, Is.EqualTo("John Doe"));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("john@example.com"));
        Assert.That(deserialized.CultureName, Is.EqualTo("en-US"));
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
        Assert.That(json, Does.Contain("userId"));
        Assert.That(json, Does.Contain("userName"));
        Assert.That(json, Does.Contain("recipientEmail"));
        Assert.That(json, Does.Contain("cultureName"));
        Assert.That(json, Does.Not.Contain("UserId"));
        Assert.That(json, Does.Not.Contain("UserName"));
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.User>(legacyUserId)));
        Assert.That(deserialized.UserName, Is.EqualTo("Legacy User"));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("legacy@example.com"));
        Assert.That(deserialized.CultureName, Is.EqualTo("de-DE"));
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(userId), "UserId not preserved");
        Assert.That(deserialized.UserName, Is.EqualTo("Test User"), "UserName not preserved");
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("test@example.com"), "RecipientEmail not preserved");
        Assert.That(deserialized.CultureName, Is.EqualTo("es-ES"), "CultureName not preserved");
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.CorrelationId.GetValue(), Is.EqualTo(userId.GetValue()));
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.InvitationId, Is.EqualTo(invitationId));
        Assert.That(deserialized.InvitationCode, Is.EqualTo("INV12345"));
        Assert.That(deserialized.ExpiresAt, Is.EqualTo(expiresAt));
        Assert.That(deserialized.TrainerName, Is.EqualTo("Coach Mike"));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("client@example.com"));
        Assert.That(deserialized.CultureName, Is.EqualTo("en-US"));
        Assert.That(deserialized.PreferredTimeZone, Is.EqualTo("America/New_York"));
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
        Assert.That(json, Does.Contain("invitationId"));
        Assert.That(json, Does.Contain("invitationCode"));
        Assert.That(json, Does.Contain("expiresAt"));
        Assert.That(json, Does.Contain("trainerName"));
        Assert.That(json, Does.Contain("recipientEmail"));
        Assert.That(json, Does.Contain("cultureName"));
        Assert.That(json, Does.Contain("preferredTimeZone"));
        Assert.That(json, Does.Not.Contain("InvitationId"));
        Assert.That(json, Does.Not.Contain("ExpiresAt"));
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.InvitationId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>(legacyInvitationId)));
        Assert.That(deserialized.InvitationCode, Is.EqualTo("LEGACY123"));
        Assert.That(deserialized.TrainerName, Is.EqualTo("Old Trainer"));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("oldclient@example.com"));
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.InvitationId, Is.EqualTo(invitationId), "InvitationId not preserved");
        Assert.That(deserialized.InvitationCode, Is.EqualTo("FULL123"), "InvitationCode not preserved");
        Assert.That(deserialized.ExpiresAt, Is.EqualTo(expiresAt), "ExpiresAt not preserved");
        Assert.That(deserialized.TrainerName, Is.EqualTo("Full Trainer"), "TrainerName not preserved");
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("fullclient@example.com"), "RecipientEmail not preserved");
        Assert.That(deserialized.CultureName, Is.EqualTo("fr-FR"), "CultureName not preserved");
        Assert.That(deserialized.PreferredTimeZone, Is.EqualTo("Europe/Paris"), "PreferredTimeZone not preserved");
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.CorrelationId.GetValue(), Is.EqualTo(invitationId.GetValue()));
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
                    ExerciseId = ParseTestId<LgymApi.Domain.Entities.Exercise>("d50e8400-e29b-41d4-a716-446655440001"),
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(userId));
        Assert.That(deserialized.TrainingId, Is.EqualTo(trainingId));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("athlete@example.com"));
        Assert.That(deserialized.CultureName, Is.EqualTo("en-US"));
        Assert.That(deserialized.PreferredTimeZone, Is.EqualTo("America/Los_Angeles"));
        Assert.That(deserialized.PlanDayName, Is.EqualTo("Upper Body A"));
        Assert.That(deserialized.TrainingDate, Is.EqualTo(trainingDate));
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
                    ExerciseId = ParseTestId<LgymApi.Domain.Entities.Exercise>("d50e8400-e29b-41d4-a716-446655440002"),
                    ExerciseName = "Bench Press",
                    Series = 1,
                    Reps = 8,
                    Weight = 80,
                    Unit = WeightUnits.Kilograms
                },
                new()
                {
                    ExerciseId = ParseTestId<LgymApi.Domain.Entities.Exercise>("d50e8400-e29b-41d4-a716-446655440003"),
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Exercises, Is.Not.Null);
        Assert.That(deserialized.Exercises.Count, Is.EqualTo(2));

        // Verify first exercise
        var firstExercise = deserialized.Exercises.First();
        Assert.That(firstExercise.ExerciseId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.Exercise>("d50e8400-e29b-41d4-a716-446655440002")));
        Assert.That(firstExercise.ExerciseName, Is.EqualTo("Bench Press"));
        Assert.That(firstExercise.Series, Is.EqualTo(1));
        Assert.That(firstExercise.Reps, Is.EqualTo(8));
        Assert.That(firstExercise.Weight, Is.EqualTo(80));
        Assert.That(firstExercise.Unit, Is.EqualTo(WeightUnits.Kilograms));

        // Verify second exercise
        var secondExercise = deserialized.Exercises.Skip(1).First();
        Assert.That(secondExercise.ExerciseId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.Exercise>("d50e8400-e29b-41d4-a716-446655440003")));
        Assert.That(secondExercise.ExerciseName, Is.EqualTo("Squat"));
        Assert.That(secondExercise.Series, Is.EqualTo(2));
        Assert.That(secondExercise.Reps, Is.EqualTo(6));
        Assert.That(secondExercise.Weight, Is.EqualTo(120));
        Assert.That(secondExercise.Unit, Is.EqualTo(WeightUnits.Kilograms));
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
        Assert.That(json, Does.Contain("userId"));
        Assert.That(json, Does.Contain("trainingId"));
        Assert.That(json, Does.Contain("recipientEmail"));
        Assert.That(json, Does.Contain("cultureName"));
        Assert.That(json, Does.Contain("preferredTimeZone"));
        Assert.That(json, Does.Contain("planDayName"));
        Assert.That(json, Does.Contain("trainingDate"));
        Assert.That(json, Does.Contain("exercises"));
        Assert.That(json, Does.Not.Contain("UserId"));
        Assert.That(json, Does.Not.Contain("Exercises"));
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
                    ExerciseId = ParseTestId<LgymApi.Domain.Entities.Exercise>("d50e8400-e29b-41d4-a716-446655440004"),
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
        Assert.That(json, Does.Contain("exerciseId"));
        Assert.That(json, Does.Contain("exerciseName"));
        Assert.That(json, Does.Contain("series"));
        Assert.That(json, Does.Contain("reps"));
        Assert.That(json, Does.Contain("weight"));
        Assert.That(json, Does.Contain("unit"));
        Assert.That(json, Does.Not.Contain("ExerciseId"));
        Assert.That(json, Does.Not.Contain("ExerciseName"));
    }

    [Test]
    public void TrainingCompletedEmailPayload_DeserializesFromLegacyPascalCaseJson()
    {
        // Arrange - simulate legacy payload with PascalCase property names
        const string legacyUserId = "c50e8400-e29b-41d4-a716-446655440003";
        const string legacyTrainingId = "c50e8400-e29b-41d4-a716-446655440004";
        const string legacyExerciseId = "d50e8400-e29b-41d4-a716-446655440005";
        var trainingDate = DateTimeOffset.UtcNow;
        var legacyJson = "{\"UserId\":\"" + legacyUserId + "\"," +
                         "\"TrainingId\":\"" + legacyTrainingId + "\"," +
                         "\"RecipientEmail\":\"legacy@example.com\"," +
                         "\"CultureName\":\"de-DE\"," +
                         "\"PreferredTimeZone\":\"Europe/Berlin\"," +
                         "\"PlanDayName\":\"Legacy Day\"," +
                         "\"TrainingDate\":\"" + trainingDate.ToString("O") + "\"," +
                         "\"Exercises\":[{\"ExerciseId\":\"" + legacyExerciseId + "\"," +
                         "\"ExerciseName\":\"Legacy Exercise\"," +
                         "\"Series\":1," +
                         "\"Reps\":5," +
                         "\"Weight\":45," +
                         "\"Unit\":\"Kilograms\"}]}";

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.User>(legacyUserId)));
        Assert.That(deserialized.TrainingId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.Training>(legacyTrainingId)));
        Assert.That(deserialized.Exercises.First().ExerciseId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.Exercise>(legacyExerciseId)));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("legacy@example.com"));
        Assert.That(deserialized.Exercises.Count, Is.EqualTo(1));
        Assert.That(deserialized.Exercises.First().ExerciseName, Is.EqualTo("Legacy Exercise"));
    }

    [Test]
    public void TrainingCompletedEmailPayload_DeserializesFromLegacyIntegerEnumValues()
    {
        // Arrange - simulate very old persisted payload with integer enum values (numeric 1 for Kilograms)
        // This represents payloads serialized before string enum enforcement
        const string legacyUserId = "c50e8400-e29b-41d4-a716-446655440005";
        const string legacyTrainingId = "c50e8400-e29b-41d4-a716-446655440006";
        const string legacyExerciseId = "d50e8400-e29b-41d4-a716-446655440006";
        var trainingDate = DateTimeOffset.UtcNow;
        var legacyJson = "{\"UserId\":\"" + legacyUserId + "\"," +
                         "\"TrainingId\":\"" + legacyTrainingId + "\"," +
                         "\"RecipientEmail\":\"ancient@example.com\"," +
                         "\"CultureName\":\"en-US\"," +
                         "\"PreferredTimeZone\":\"UTC\"," +
                         "\"PlanDayName\":\"Ancient Day\"," +
                         "\"TrainingDate\":\"" + trainingDate.ToString("O") + "\"," +
                         "\"Exercises\":[{\"ExerciseId\":\"" + legacyExerciseId + "\"," +
                         "\"ExerciseName\":\"Ancient Exercise\"," +
                         "\"Series\":1," +
                         "\"Reps\":8," +
                         "\"Weight\":50," +
                         "\"Unit\":1}]}";  // 1 = Kilograms enum value

        // Act
        var deserialized = JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.User>(legacyUserId)));
        Assert.That(deserialized.TrainingId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.Training>(legacyTrainingId)));
        Assert.That(deserialized.Exercises.First().ExerciseId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.Exercise>(legacyExerciseId)));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("ancient@example.com"));
        Assert.That(deserialized.Exercises.Count, Is.EqualTo(1));
        Assert.That(deserialized.Exercises.First().ExerciseName, Is.EqualTo("Ancient Exercise"));
        // Verify the enum was properly deserialized from integer value
        Assert.That(deserialized.Exercises.First().Unit, Is.EqualTo(WeightUnits.Kilograms));
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
                    ExerciseId = ParseTestId<LgymApi.Domain.Entities.Exercise>("d50e8400-e29b-41d4-a716-446655440007"),
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.UserId, Is.EqualTo(userId), "UserId not preserved");
        Assert.That(deserialized.TrainingId, Is.EqualTo(trainingId), "TrainingId not preserved");
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("full@example.com"), "RecipientEmail not preserved");
        Assert.That(deserialized.CultureName, Is.EqualTo("es-ES"), "CultureName not preserved");
        Assert.That(deserialized.PreferredTimeZone, Is.EqualTo("Europe/Madrid"), "PreferredTimeZone not preserved");
        Assert.That(deserialized.PlanDayName, Is.EqualTo("Full Upper"), "PlanDayName not preserved");
        Assert.That(deserialized.TrainingDate, Is.EqualTo(trainingDate), "TrainingDate not preserved");
        Assert.That(deserialized.Exercises.Count, Is.EqualTo(1), "Exercises count not preserved");
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.CorrelationId.GetValue(), Is.EqualTo(trainingId.GetValue()));
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
        Assert.That(deserializedPayload, Is.Not.Null);
        Assert.That(deserializedPayload!.UserName, Is.EqualTo("Composer Test"));
        Assert.That(deserializedPayload.RecipientEmail, Is.EqualTo("composer@example.com"));
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
        Assert.That(deserializedPayload, Is.Not.Null);
        Assert.That(deserializedPayload!.InvitationCode, Is.EqualTo("COMP123"));
        Assert.That(deserializedPayload.TrainerName, Is.EqualTo("Composer Trainer"));
        Assert.That(deserializedPayload.ExpiresAt, Is.EqualTo(expiresAt));
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
                    ExerciseId = ParseTestId<LgymApi.Domain.Entities.Exercise>("d50e8400-e29b-41d4-a716-446655440008"),
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
        Assert.That(deserializedPayload, Is.Not.Null);
        Assert.That(deserializedPayload!.PlanDayName, Is.EqualTo("Composer Training"));
        Assert.That(deserializedPayload.Exercises.Count, Is.EqualTo(1));
        Assert.That(deserializedPayload.Exercises.First().ExerciseName, Is.EqualTo("Composer Exercise"));
    }

    [Test]
    public void TrainingCompletedEmailPayload_ExerciseId_RemainsUuidStringInJson()
    {
        const string exerciseIdValue = "d50e8400-e29b-41d4-a716-446655440009";
        var payload = new TrainingCompletedEmailPayload
        {
            UserId = Id<LgymApi.Domain.Entities.User>.New(),
            TrainingId = Id<LgymApi.Domain.Entities.Training>.New(),
            Exercises =
            [
                new TrainingExerciseSummary
                {
                    ExerciseId = ParseTestId<LgymApi.Domain.Entities.Exercise>(exerciseIdValue),
                    ExerciseName = "Bench Press"
                }
            ]
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload, SharedSerializationOptions.Current));

        Assert.That(document.RootElement.GetProperty("exercises")[0].GetProperty("exerciseId").GetString(), Is.EqualTo(exerciseIdValue));
    }

    [Test]
    public void TrainingCompletedEmailPayload_InvalidLegacyExerciseId_ThrowsJsonException()
    {
        const string legacyJson = "{\"exercises\":[{\"exerciseId\":\"not-a-uuid\"}]}";

        Assert.That(() => { JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(legacyJson, SharedSerializationOptions.Current); }, Throws.InstanceOf<JsonException>());
    }

    #endregion

    #region Frozen Wave-1 Email Contract Fixtures

    [Test]
    public void WelcomeEmailPayload_FixedFixture_PreservesWireContract()
    {
        var userId = ParseTestId<LgymApi.Domain.Entities.User>("11111111-1111-1111-1111-111111111111");
        var fixture = new WelcomeEmailPayload
        {
            UserId = userId,
            UserName = "Wave One Welcome",
            RecipientEmail = "welcome.contract@example.test",
            CultureName = MalformedCultureName
        };

        var roundtrip = SerializeAndDeserialize(fixture);

        Assert.That(roundtrip.Json, Does.Contain($"\"userId\":\"{userId.GetValue()}\""));
        Assert.That(roundtrip.Json, Does.Contain("\"userName\":\"Wave One Welcome\""));
        Assert.That(roundtrip.Json, Does.Contain("\"recipientEmail\":\"welcome.contract@example.test\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"UserId\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"culture\""));
        Assert.That(roundtrip.Payload.UserId, Is.EqualTo(userId));
        Assert.That(roundtrip.Payload.UserName, Is.EqualTo("Wave One Welcome"));
        Assert.That(roundtrip.Payload.RecipientEmail, Is.EqualTo("welcome.contract@example.test"));
        Assert.That(roundtrip.Payload.CultureName, Is.EqualTo(MalformedCultureName));
        AssertPayloadMetadata(roundtrip.Payload, userId.Rebind<LgymApi.Domain.ValueObjects.CorrelationScope>(), "user.registration.welcome");
    }

    [Test]
    public void InvitationEmailPayload_FixedFixture_PreservesWireContract()
    {
        var invitationId = ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("22222222-2222-2222-2222-222222222222");
        var expiresAt = new DateTimeOffset(2026, 8, 1, 12, 30, 0, TimeSpan.Zero);
        var fixture = new InvitationEmailPayload
        {
            InvitationId = invitationId,
            InvitationCode = "INV-380-FIXTURE",
            ExpiresAt = expiresAt,
            TrainerName = "Coach Contract",
            RecipientEmail = "invitation.contract@example.test",
            CultureName = MalformedCultureName,
            PreferredTimeZone = "Europe/Warsaw"
        };

        var roundtrip = SerializeAndDeserialize(fixture);

        Assert.That(roundtrip.Json, Does.Contain($"\"invitationId\":\"{invitationId.GetValue()}\""));
        Assert.That(roundtrip.Json, Does.Contain("\"invitationCode\":\"INV-380-FIXTURE\""));
        Assert.That(roundtrip.Json, Does.Contain("\"preferredTimeZone\":\"Europe/Warsaw\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"InvitationId\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"culture\""));
        Assert.That(roundtrip.Payload.InvitationId, Is.EqualTo(invitationId));
        Assert.That(roundtrip.Payload.InvitationCode, Is.EqualTo("INV-380-FIXTURE"));
        Assert.That(roundtrip.Payload.ExpiresAt, Is.EqualTo(expiresAt));
        Assert.That(roundtrip.Payload.TrainerName, Is.EqualTo("Coach Contract"));
        Assert.That(roundtrip.Payload.RecipientEmail, Is.EqualTo("invitation.contract@example.test"));
        Assert.That(roundtrip.Payload.CultureName, Is.EqualTo(MalformedCultureName));
        Assert.That(roundtrip.Payload.PreferredTimeZone, Is.EqualTo("Europe/Warsaw"));
        AssertPayloadMetadata(roundtrip.Payload, invitationId.Rebind<LgymApi.Domain.ValueObjects.CorrelationScope>(), "trainer.invitation.created");
    }

    [Test]
    public void InvitationAcceptedEmailPayload_FixedFixture_PreservesWireContract()
    {
        var invitationId = ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("33333333-3333-3333-3333-333333333333");
        var fixture = new InvitationAcceptedEmailPayload
        {
            InvitationId = invitationId,
            TrainerName = "Coach Accepted",
            TraineeName = "Trainee Accepted",
            RecipientEmail = "accepted.contract@example.test",
            CultureName = MalformedCultureName,
            PreferredTimeZone = "America/Toronto"
        };

        var roundtrip = SerializeAndDeserialize(fixture);

        Assert.That(roundtrip.Json, Does.Contain($"\"invitationId\":\"{invitationId.GetValue()}\""));
        Assert.That(roundtrip.Json, Does.Contain("\"trainerName\":\"Coach Accepted\""));
        Assert.That(roundtrip.Json, Does.Contain("\"traineeName\":\"Trainee Accepted\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"InvitationId\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"culture\""));
        Assert.That(roundtrip.Payload.InvitationId, Is.EqualTo(invitationId));
        Assert.That(roundtrip.Payload.TrainerName, Is.EqualTo("Coach Accepted"));
        Assert.That(roundtrip.Payload.TraineeName, Is.EqualTo("Trainee Accepted"));
        Assert.That(roundtrip.Payload.RecipientEmail, Is.EqualTo("accepted.contract@example.test"));
        Assert.That(roundtrip.Payload.CultureName, Is.EqualTo(MalformedCultureName));
        Assert.That(roundtrip.Payload.PreferredTimeZone, Is.EqualTo("America/Toronto"));
        AssertPayloadMetadata(roundtrip.Payload, invitationId.Rebind<LgymApi.Domain.ValueObjects.CorrelationScope>(), "trainer.invitation.accepted");
    }

    [Test]
    public void InvitationRevokedEmailPayload_FixedFixture_PreservesWireContract()
    {
        var invitationId = ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("44444444-4444-4444-4444-444444444444");
        var fixture = new InvitationRevokedEmailPayload
        {
            InvitationId = invitationId,
            TrainerName = "Coach Revoked",
            RecipientEmail = "revoked.contract@example.test",
            CultureName = MalformedCultureName,
            PreferredTimeZone = "Australia/Sydney"
        };

        var roundtrip = SerializeAndDeserialize(fixture);

        Assert.That(roundtrip.Json, Does.Contain($"\"invitationId\":\"{invitationId.GetValue()}\""));
        Assert.That(roundtrip.Json, Does.Contain("\"trainerName\":\"Coach Revoked\""));
        Assert.That(roundtrip.Json, Does.Contain("\"preferredTimeZone\":\"Australia/Sydney\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"InvitationId\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"culture\""));
        Assert.That(roundtrip.Payload.InvitationId, Is.EqualTo(invitationId));
        Assert.That(roundtrip.Payload.TrainerName, Is.EqualTo("Coach Revoked"));
        Assert.That(roundtrip.Payload.RecipientEmail, Is.EqualTo("revoked.contract@example.test"));
        Assert.That(roundtrip.Payload.CultureName, Is.EqualTo(MalformedCultureName));
        Assert.That(roundtrip.Payload.PreferredTimeZone, Is.EqualTo("Australia/Sydney"));
        AssertPayloadMetadata(roundtrip.Payload, invitationId.Rebind<LgymApi.Domain.ValueObjects.CorrelationScope>(), "trainer.invitation.revoked");
    }

    [Test]
    public void TrainingCompletedEmailPayload_FixedFixture_PreservesNestedExerciseAndEnumContract()
    {
        var userId = ParseTestId<LgymApi.Domain.Entities.User>("55555555-5555-5555-5555-555555555555");
        var trainingId = ParseTestId<LgymApi.Domain.Entities.Training>("66666666-6666-6666-6666-666666666666");
        var exerciseId = ParseTestId<LgymApi.Domain.Entities.Exercise>("77777777-7777-7777-7777-777777777777");
        var trainingDate = new DateTimeOffset(2026, 8, 2, 6, 45, 0, TimeSpan.Zero);
        var fixture = new TrainingCompletedEmailPayload
        {
            UserId = userId,
            TrainingId = trainingId,
            RecipientEmail = "training.contract@example.test",
            CultureName = MalformedCultureName,
            PreferredTimeZone = "America/Los_Angeles",
            PlanDayName = "Wave One Strength",
            TrainingDate = trainingDate,
            Exercises =
            [
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId,
                    ExerciseName = "Contract Deadlift",
                    Series = 4,
                    Reps = 5.5,
                    Weight = 140.25,
                    Unit = WeightUnits.Pounds
                }
            ]
        };

        var roundtrip = SerializeAndDeserialize(fixture);

        Assert.That(roundtrip.Json, Does.Contain($"\"userId\":\"{userId.GetValue()}\""));
        Assert.That(roundtrip.Json, Does.Contain($"\"trainingId\":\"{trainingId.GetValue()}\""));
        Assert.That(roundtrip.Json, Does.Contain($"\"exerciseId\":\"{exerciseId.GetValue()}\""));
        Assert.That(roundtrip.Json, Does.Contain("\"exerciseName\":\"Contract Deadlift\""));
        Assert.That(roundtrip.Json, Does.Contain("\"unit\":\"Pounds\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"TrainingId\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"culture\""));
        Assert.That(roundtrip.Payload.UserId, Is.EqualTo(userId));
        Assert.That(roundtrip.Payload.TrainingId, Is.EqualTo(trainingId));
        Assert.That(roundtrip.Payload.RecipientEmail, Is.EqualTo("training.contract@example.test"));
        Assert.That(roundtrip.Payload.CultureName, Is.EqualTo(MalformedCultureName));
        Assert.That(roundtrip.Payload.PreferredTimeZone, Is.EqualTo("America/Los_Angeles"));
        Assert.That(roundtrip.Payload.PlanDayName, Is.EqualTo("Wave One Strength"));
        Assert.That(roundtrip.Payload.TrainingDate, Is.EqualTo(trainingDate));
        Assert.That(roundtrip.Payload.Exercises, Has.Exactly(1).Items);
        Assert.That(roundtrip.Payload.Exercises[0].ExerciseId, Is.EqualTo(exerciseId));
        Assert.That(roundtrip.Payload.Exercises[0].ExerciseName, Is.EqualTo("Contract Deadlift"));
        Assert.That(roundtrip.Payload.Exercises[0].Series, Is.EqualTo(4));
        Assert.That(roundtrip.Payload.Exercises[0].Reps, Is.EqualTo(5.5));
        Assert.That(roundtrip.Payload.Exercises[0].Weight, Is.EqualTo(140.25));
        Assert.That(roundtrip.Payload.Exercises[0].Unit, Is.EqualTo(WeightUnits.Pounds));
        AssertPayloadMetadata(roundtrip.Payload, trainingId.Rebind<LgymApi.Domain.ValueObjects.CorrelationScope>(), "training.completed");
    }

    [Test]
    public void PasswordRecoveryEmailPayload_FixedFixture_PreservesWireContract()
    {
        var userId = ParseTestId<LgymApi.Domain.Entities.User>("88888888-8888-8888-8888-888888888888");
        var tokenId = ParseTestId<LgymApi.Domain.Entities.PasswordResetToken>("99999999-9999-9999-9999-999999999999");
        var fixture = new PasswordRecoveryEmailPayload
        {
            UserId = userId,
            TokenId = tokenId,
            UserName = "Recovery Contract",
            RecipientEmail = "recovery.contract@example.test",
            ResetToken = "reset-token-380",
            ResetUrl = "https://request-sentinel.example.test/reset",
            CultureName = MalformedCultureName
        };

        var roundtrip = SerializeAndDeserialize(fixture);

        Assert.That(roundtrip.Json, Does.Contain($"\"userId\":\"{userId.GetValue()}\""));
        Assert.That(roundtrip.Json, Does.Contain($"\"tokenId\":\"{tokenId.GetValue()}\""));
        Assert.That(roundtrip.Json, Does.Contain("\"resetToken\":\"reset-token-380\""));
        Assert.That(roundtrip.Json, Does.Contain("\"resetUrl\":\"https://request-sentinel.example.test/reset\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"TokenId\""));
        Assert.That(roundtrip.Json, Does.Not.Contain("\"culture\""));
        Assert.That(roundtrip.Payload.UserId, Is.EqualTo(userId));
        Assert.That(roundtrip.Payload.TokenId, Is.EqualTo(tokenId));
        Assert.That(roundtrip.Payload.UserName, Is.EqualTo("Recovery Contract"));
        Assert.That(roundtrip.Payload.RecipientEmail, Is.EqualTo("recovery.contract@example.test"));
        Assert.That(roundtrip.Payload.ResetToken, Is.EqualTo("reset-token-380"));
        Assert.That(roundtrip.Payload.ResetUrl, Is.EqualTo("https://request-sentinel.example.test/reset"));
        Assert.That(roundtrip.Payload.CultureName, Is.EqualTo(MalformedCultureName));
        AssertPayloadMetadata(roundtrip.Payload, tokenId.Rebind<LgymApi.Domain.ValueObjects.CorrelationScope>(), "user.password.recovery");
    }

    [Test]
    public void PasswordRecoveryEmailPayload_NullResetUrl_IsOmittedFromWireJson()
    {
        var fixture = new PasswordRecoveryEmailPayload
        {
            UserId = ParseTestId<LgymApi.Domain.Entities.User>("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            TokenId = ParseTestId<LgymApi.Domain.Entities.PasswordResetToken>("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            UserName = "Null Omission",
            RecipientEmail = "null.contract@example.test",
            ResetToken = "null-reset-token",
            ResetUrl = null!,
            CultureName = "en-US"
        };

        var json = JsonSerializer.Serialize(fixture, SharedSerializationOptions.Current);

        Assert.That(json, Does.Not.Contain("\"resetUrl\""));
        Assert.That(json, Does.Contain("\"resetToken\":\"null-reset-token\""));
    }

    [Test]
    public void InvitationAcceptedEmailPayload_DeserializesLegacyPascalCaseJson()
    {
        const string invitationId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
        const string payloadJson = """{"InvitationId":"cccccccc-cccc-cccc-cccc-cccccccccccc","TrainerName":"Legacy Coach","TraineeName":"Legacy Trainee","RecipientEmail":"accepted.legacy@example.test","CultureName":"en-US","PreferredTimeZone":"UTC"}""";

        var payload = JsonSerializer.Deserialize<InvitationAcceptedEmailPayload>(payloadJson, SharedSerializationOptions.Current);

        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.InvitationId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>(invitationId)));
        Assert.That(payload.TrainerName, Is.EqualTo("Legacy Coach"));
        Assert.That(payload.TraineeName, Is.EqualTo("Legacy Trainee"));
        Assert.That(payload.RecipientEmail, Is.EqualTo("accepted.legacy@example.test"));
    }

    [Test]
    public void InvitationRevokedEmailPayload_DeserializesMixedCaseJson()
    {
        const string invitationId = "dddddddd-dddd-dddd-dddd-dddddddddddd";
        const string payloadJson = """{"iNvItAtIoNiD":"dddddddd-dddd-dddd-dddd-dddddddddddd","tRaInErNaMe":"Mixed Coach","rEcIpIeNtEmAiL":"revoked.mixed@example.test","cUlTuReNaMe":"en-US","pReFeRrEdTiMeZoNe":"UTC"}""";

        var payload = JsonSerializer.Deserialize<InvitationRevokedEmailPayload>(payloadJson, SharedSerializationOptions.Current);

        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.InvitationId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>(invitationId)));
        Assert.That(payload.TrainerName, Is.EqualTo("Mixed Coach"));
        Assert.That(payload.RecipientEmail, Is.EqualTo("revoked.mixed@example.test"));
        Assert.That(payload.PreferredTimeZone, Is.EqualTo("UTC"));
    }

    [Test]
    public void PasswordRecoveryEmailPayload_DeserializesMixedCaseJson()
    {
        const string userId = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";
        const string tokenId = "ffffffff-ffff-ffff-ffff-ffffffffffff";
        const string payloadJson = """{"uSeRiD":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","tOkEnId":"ffffffff-ffff-ffff-ffff-ffffffffffff","uSeRnAmE":"Mixed Recovery","rEcIpIeNtEmAiL":"recovery.mixed@example.test","rEsEtToKeN":"mixed-token","rEsEtUrL":"https://mixed.example.test/reset","cUlTuReNaMe":"en-US"}""";

        var payload = JsonSerializer.Deserialize<PasswordRecoveryEmailPayload>(payloadJson, SharedSerializationOptions.Current);

        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.UserId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.User>(userId)));
        Assert.That(payload.TokenId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.PasswordResetToken>(tokenId)));
        Assert.That(payload.UserName, Is.EqualTo("Mixed Recovery"));
        Assert.That(payload.RecipientEmail, Is.EqualTo("recovery.mixed@example.test"));
        Assert.That(payload.ResetToken, Is.EqualTo("mixed-token"));
        Assert.That(payload.ResetUrl, Is.EqualTo("https://mixed.example.test/reset"));
    }

    [TestCaseSource(nameof(CreateGoldenPayloadCases))]
    public void EmailPayload_GoldenWireMatrix_PreservesEveryPayloadContract(EmailPayloadGoldenCase goldenCase)
    {
        var evaluation = EvaluateGoldenCase(goldenCase);

        goldenCase.AssertWire(evaluation.Wire.RootElement);
        goldenCase.AssertPayload(evaluation.RoundtrippedPayload);
        goldenCase.AssertPayload(evaluation.LegacyPayload);
        Assert.That(evaluation.Wire.RootElement.TryGetProperty("culture", out _), Is.False);
        Assert.That(evaluation.NullWire.RootElement.TryGetProperty(goldenCase.NullPropertyName, out _), Is.False);
    }

    private static IEnumerable<TestCaseData> CreateGoldenPayloadCases()
    {
        yield return new TestCaseData(new EmailPayloadGoldenCase(
            "welcome",
            new WelcomeEmailPayload
            {
                UserId = ParseTestId<LgymApi.Domain.Entities.User>("10101010-1010-1010-1010-101010101010"),
                UserName = "Golden Welcome",
                RecipientEmail = "golden.welcome@example.test",
                CultureName = MalformedCultureName
            },
            new WelcomeEmailPayload
            {
                UserId = ParseTestId<LgymApi.Domain.Entities.User>("10101010-1010-1010-1010-101010101010"),
                UserName = null!,
                RecipientEmail = "golden.welcome@example.test",
                CultureName = MalformedCultureName
            },
            "userName",
            """{"UsErId":"10101010-1010-1010-1010-101010101010","uSeRnAmE":"Golden Welcome","ReCiPiEnTeMaIl":"golden.welcome@example.test","CuLtUrEnAmE":"!invalid-culture-380"}""",
            root =>
            {
                AssertCamelCaseProperties(root, "userId", "userName", "recipientEmail", "cultureName");
                Assert.That(root.GetProperty("userId").GetString(), Is.EqualTo("10101010-1010-1010-1010-101010101010"));
            },
            payload =>
            {
                var assertedValue103 = payload;
                Assert.That(assertedValue103, Is.TypeOf<WelcomeEmailPayload>());
                var actual = ((WelcomeEmailPayload)assertedValue103);
                Assert.That(actual.UserId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.User>("10101010-1010-1010-1010-101010101010")));
                Assert.That(actual.UserName, Is.EqualTo("Golden Welcome"));
                Assert.That(actual.RecipientEmail, Is.EqualTo("golden.welcome@example.test"));
                Assert.That(actual.CultureName, Is.EqualTo(MalformedCultureName));
                AssertPayloadMetadata(actual, ParseTestId<LgymApi.Domain.ValueObjects.CorrelationScope>("10101010-1010-1010-1010-101010101010"), "user.registration.welcome");
            })).SetName("Email payload golden matrix: Welcome");

        yield return new TestCaseData(new EmailPayloadGoldenCase(
            "invitation",
            new InvitationEmailPayload
            {
                InvitationId = ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("20202020-2020-2020-2020-202020202020"),
                InvitationCode = "GOLDEN-INVITATION",
                ExpiresAt = new DateTimeOffset(2026, 8, 1, 12, 30, 0, TimeSpan.Zero),
                TrainerName = "Golden Coach",
                RecipientEmail = "golden.invitation@example.test",
                CultureName = MalformedCultureName,
                PreferredTimeZone = "Europe/Warsaw"
            },
            new InvitationEmailPayload
            {
                InvitationId = ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("20202020-2020-2020-2020-202020202020"),
                InvitationCode = "GOLDEN-INVITATION",
                ExpiresAt = new DateTimeOffset(2026, 8, 1, 12, 30, 0, TimeSpan.Zero),
                TrainerName = null!,
                RecipientEmail = "golden.invitation@example.test",
                CultureName = MalformedCultureName,
                PreferredTimeZone = "Europe/Warsaw"
            },
            "trainerName",
            """{"InvitationId":"20202020-2020-2020-2020-202020202020","iNvItAtIoNcOdE":"GOLDEN-INVITATION","ExpiresAt":"2026-08-01T12:30:00+00:00","TrAiNeRnAmE":"Golden Coach","RecipientEmail":"golden.invitation@example.test","CultureName":"!invalid-culture-380","PreferredTimeZone":"Europe/Warsaw"}""",
            root =>
            {
                AssertCamelCaseProperties(root, "invitationId", "invitationCode", "expiresAt", "trainerName", "recipientEmail", "cultureName", "preferredTimeZone");
                Assert.That(root.GetProperty("invitationId").GetString(), Is.EqualTo("20202020-2020-2020-2020-202020202020"));
            },
            payload =>
            {
                var assertedValue104 = payload;
                Assert.That(assertedValue104, Is.TypeOf<InvitationEmailPayload>());
                var actual = ((InvitationEmailPayload)assertedValue104);
                Assert.That(actual.InvitationId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("20202020-2020-2020-2020-202020202020")));
                Assert.That(actual.InvitationCode, Is.EqualTo("GOLDEN-INVITATION"));
                Assert.That(actual.ExpiresAt, Is.EqualTo(new DateTimeOffset(2026, 8, 1, 12, 30, 0, TimeSpan.Zero)));
                Assert.That(actual.TrainerName, Is.EqualTo("Golden Coach"));
                Assert.That(actual.RecipientEmail, Is.EqualTo("golden.invitation@example.test"));
                Assert.That(actual.CultureName, Is.EqualTo(MalformedCultureName));
                Assert.That(actual.PreferredTimeZone, Is.EqualTo("Europe/Warsaw"));
                AssertPayloadMetadata(actual, ParseTestId<LgymApi.Domain.ValueObjects.CorrelationScope>("20202020-2020-2020-2020-202020202020"), "trainer.invitation.created");
            })).SetName("Email payload golden matrix: Invitation");

        yield return new TestCaseData(new EmailPayloadGoldenCase(
            "invitation accepted",
            new InvitationAcceptedEmailPayload
            {
                InvitationId = ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("30303030-3030-3030-3030-303030303030"),
                TrainerName = "Golden Accepted Coach",
                TraineeName = "Golden Accepted Trainee",
                RecipientEmail = "golden.accepted@example.test",
                CultureName = MalformedCultureName,
                PreferredTimeZone = "America/Toronto"
            },
            new InvitationAcceptedEmailPayload
            {
                InvitationId = ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("30303030-3030-3030-3030-303030303030"),
                TrainerName = "Golden Accepted Coach",
                TraineeName = null!,
                RecipientEmail = "golden.accepted@example.test",
                CultureName = MalformedCultureName,
                PreferredTimeZone = "America/Toronto"
            },
            "traineeName",
            """{"iNvItAtIoNiD":"30303030-3030-3030-3030-303030303030","TrainerName":"Golden Accepted Coach","tRaInEeNaMe":"Golden Accepted Trainee","RecipientEmail":"golden.accepted@example.test","CultureName":"!invalid-culture-380","PreferredTimeZone":"America/Toronto"}""",
            root =>
            {
                AssertCamelCaseProperties(root, "invitationId", "trainerName", "traineeName", "recipientEmail", "cultureName", "preferredTimeZone");
                Assert.That(root.GetProperty("invitationId").GetString(), Is.EqualTo("30303030-3030-3030-3030-303030303030"));
            },
            payload =>
            {
                var assertedValue105 = payload;
                Assert.That(assertedValue105, Is.TypeOf<InvitationAcceptedEmailPayload>());
                var actual = ((InvitationAcceptedEmailPayload)assertedValue105);
                Assert.That(actual.InvitationId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("30303030-3030-3030-3030-303030303030")));
                Assert.That(actual.TrainerName, Is.EqualTo("Golden Accepted Coach"));
                Assert.That(actual.TraineeName, Is.EqualTo("Golden Accepted Trainee"));
                Assert.That(actual.RecipientEmail, Is.EqualTo("golden.accepted@example.test"));
                Assert.That(actual.CultureName, Is.EqualTo(MalformedCultureName));
                Assert.That(actual.PreferredTimeZone, Is.EqualTo("America/Toronto"));
                AssertPayloadMetadata(actual, ParseTestId<LgymApi.Domain.ValueObjects.CorrelationScope>("30303030-3030-3030-3030-303030303030"), "trainer.invitation.accepted");
            })).SetName("Email payload golden matrix: Invitation accepted");

        yield return new TestCaseData(new EmailPayloadGoldenCase(
            "invitation revoked",
            new InvitationRevokedEmailPayload
            {
                InvitationId = ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("40404040-4040-4040-4040-404040404040"),
                TrainerName = "Golden Revoked Coach",
                RecipientEmail = "golden.revoked@example.test",
                CultureName = MalformedCultureName,
                PreferredTimeZone = "Australia/Sydney"
            },
            new InvitationRevokedEmailPayload
            {
                InvitationId = ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("40404040-4040-4040-4040-404040404040"),
                TrainerName = null!,
                RecipientEmail = "golden.revoked@example.test",
                CultureName = MalformedCultureName,
                PreferredTimeZone = "Australia/Sydney"
            },
            "trainerName",
            """{"InvitationId":"40404040-4040-4040-4040-404040404040","tRaInErNaMe":"Golden Revoked Coach","rEcIpIeNtEmAiL":"golden.revoked@example.test","cUlTuReNaMe":"!invalid-culture-380","pReFeRrEdTiMeZoNe":"Australia/Sydney"}""",
            root =>
            {
                AssertCamelCaseProperties(root, "invitationId", "trainerName", "recipientEmail", "cultureName", "preferredTimeZone");
                Assert.That(root.GetProperty("invitationId").GetString(), Is.EqualTo("40404040-4040-4040-4040-404040404040"));
            },
            payload =>
            {
                var assertedValue106 = payload;
                Assert.That(assertedValue106, Is.TypeOf<InvitationRevokedEmailPayload>());
                var actual = ((InvitationRevokedEmailPayload)assertedValue106);
                Assert.That(actual.InvitationId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.TrainerInvitation>("40404040-4040-4040-4040-404040404040")));
                Assert.That(actual.TrainerName, Is.EqualTo("Golden Revoked Coach"));
                Assert.That(actual.RecipientEmail, Is.EqualTo("golden.revoked@example.test"));
                Assert.That(actual.CultureName, Is.EqualTo(MalformedCultureName));
                Assert.That(actual.PreferredTimeZone, Is.EqualTo("Australia/Sydney"));
                AssertPayloadMetadata(actual, ParseTestId<LgymApi.Domain.ValueObjects.CorrelationScope>("40404040-4040-4040-4040-404040404040"), "trainer.invitation.revoked");
            })).SetName("Email payload golden matrix: Invitation revoked");

        yield return new TestCaseData(new EmailPayloadGoldenCase(
            "training completed",
            new TrainingCompletedEmailPayload
            {
                UserId = ParseTestId<LgymApi.Domain.Entities.User>("50505050-5050-5050-5050-505050505050"),
                TrainingId = ParseTestId<LgymApi.Domain.Entities.Training>("60606060-6060-6060-6060-606060606060"),
                RecipientEmail = "golden.training@example.test",
                CultureName = MalformedCultureName,
                PreferredTimeZone = "America/Los_Angeles",
                PlanDayName = "Golden Strength",
                TrainingDate = new DateTimeOffset(2026, 8, 2, 6, 45, 0, TimeSpan.Zero),
                Exercises =
                [
                    new TrainingExerciseSummary
                    {
                        ExerciseId = ParseTestId<LgymApi.Domain.Entities.Exercise>("70707070-7070-7070-7070-707070707070"),
                        ExerciseName = "Golden Deadlift",
                        Series = 4,
                        Reps = 5.5,
                        Weight = 140.25,
                        Unit = WeightUnits.Pounds
                    }
                ]
            },
            new TrainingCompletedEmailPayload
            {
                UserId = ParseTestId<LgymApi.Domain.Entities.User>("50505050-5050-5050-5050-505050505050"),
                TrainingId = ParseTestId<LgymApi.Domain.Entities.Training>("60606060-6060-6060-6060-606060606060"),
                RecipientEmail = "golden.training@example.test",
                CultureName = MalformedCultureName,
                PreferredTimeZone = "America/Los_Angeles",
                PlanDayName = null!,
                TrainingDate = new DateTimeOffset(2026, 8, 2, 6, 45, 0, TimeSpan.Zero),
                Exercises = Array.Empty<TrainingExerciseSummary>()
            },
            "planDayName",
            """{"UsErId":"50505050-5050-5050-5050-505050505050","tRaInInGiD":"60606060-6060-6060-6060-606060606060","RecipientEmail":"golden.training@example.test","CultureName":"!invalid-culture-380","PreferredTimeZone":"America/Los_Angeles","PlanDayName":"Golden Strength","TrainingDate":"2026-08-02T06:45:00+00:00","Exercises":[{"ExerciseId":"70707070-7070-7070-7070-707070707070","ExerciseName":"Golden Deadlift","Series":4,"Reps":5.5,"Weight":140.25,"Unit":2}]}""",
            root =>
            {
                AssertCamelCaseProperties(root, "userId", "trainingId", "recipientEmail", "cultureName", "preferredTimeZone", "planDayName", "trainingDate", "exercises");
                Assert.That(root.GetProperty("userId").GetString(), Is.EqualTo("50505050-5050-5050-5050-505050505050"));
                Assert.That(root.GetProperty("trainingId").GetString(), Is.EqualTo("60606060-6060-6060-6060-606060606060"));
                var exercise = root.GetProperty("exercises")[0];
                AssertCamelCaseProperties(exercise, "exerciseId", "exerciseName", "series", "reps", "weight", "unit");
                Assert.That(exercise.GetProperty("exerciseId").GetString(), Is.EqualTo("70707070-7070-7070-7070-707070707070"));
                Assert.That(exercise.GetProperty("unit").GetString(), Is.EqualTo("Pounds"));
            },
            payload =>
            {
                var assertedValue107 = payload;
                Assert.That(assertedValue107, Is.TypeOf<TrainingCompletedEmailPayload>());
                var actual = ((TrainingCompletedEmailPayload)assertedValue107);
                Assert.That(actual.UserId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.User>("50505050-5050-5050-5050-505050505050")));
                Assert.That(actual.TrainingId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.Training>("60606060-6060-6060-6060-606060606060")));
                Assert.That(actual.RecipientEmail, Is.EqualTo("golden.training@example.test"));
                Assert.That(actual.CultureName, Is.EqualTo(MalformedCultureName));
                Assert.That(actual.PreferredTimeZone, Is.EqualTo("America/Los_Angeles"));
                Assert.That(actual.PlanDayName, Is.EqualTo("Golden Strength"));
                Assert.That(actual.TrainingDate, Is.EqualTo(new DateTimeOffset(2026, 8, 2, 6, 45, 0, TimeSpan.Zero)));
                Assert.That(actual.Exercises, Has.Exactly(1).Items);
                Assert.That(actual.Exercises[0].ExerciseId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.Exercise>("70707070-7070-7070-7070-707070707070")));
                Assert.That(actual.Exercises[0].ExerciseName, Is.EqualTo("Golden Deadlift"));
                Assert.That(actual.Exercises[0].Series, Is.EqualTo(4));
                Assert.That(actual.Exercises[0].Reps, Is.EqualTo(5.5));
                Assert.That(actual.Exercises[0].Weight, Is.EqualTo(140.25));
                Assert.That(actual.Exercises[0].Unit, Is.EqualTo(WeightUnits.Pounds));
                AssertPayloadMetadata(actual, ParseTestId<LgymApi.Domain.ValueObjects.CorrelationScope>("60606060-6060-6060-6060-606060606060"), "training.completed");
            })).SetName("Email payload golden matrix: Training completed");

        yield return new TestCaseData(new EmailPayloadGoldenCase(
            "password recovery",
            new PasswordRecoveryEmailPayload
            {
                UserId = ParseTestId<LgymApi.Domain.Entities.User>("80808080-8080-8080-8080-808080808080"),
                TokenId = ParseTestId<LgymApi.Domain.Entities.PasswordResetToken>("90909090-9090-9090-9090-909090909090"),
                UserName = "Golden Recovery",
                RecipientEmail = "golden.recovery@example.test",
                ResetToken = "golden-reset-token",
                ResetUrl = "https://golden-sentinel.example.test/reset",
                CultureName = MalformedCultureName
            },
            new PasswordRecoveryEmailPayload
            {
                UserId = ParseTestId<LgymApi.Domain.Entities.User>("80808080-8080-8080-8080-808080808080"),
                TokenId = ParseTestId<LgymApi.Domain.Entities.PasswordResetToken>("90909090-9090-9090-9090-909090909090"),
                UserName = "Golden Recovery",
                RecipientEmail = "golden.recovery@example.test",
                ResetToken = "golden-reset-token",
                ResetUrl = null!,
                CultureName = MalformedCultureName
            },
            "resetUrl",
            """{"uSeRiD":"80808080-8080-8080-8080-808080808080","ToKeNiD":"90909090-9090-9090-9090-909090909090","UserName":"Golden Recovery","rEcIpIeNtEmAiL":"golden.recovery@example.test","ResetToken":"golden-reset-token","ResetUrl":"https://golden-sentinel.example.test/reset","CultureName":"!invalid-culture-380"}""",
            root =>
            {
                AssertCamelCaseProperties(root, "userId", "tokenId", "userName", "recipientEmail", "resetToken", "resetUrl", "cultureName");
                Assert.That(root.GetProperty("userId").GetString(), Is.EqualTo("80808080-8080-8080-8080-808080808080"));
                Assert.That(root.GetProperty("tokenId").GetString(), Is.EqualTo("90909090-9090-9090-9090-909090909090"));
            },
            payload =>
            {
                var assertedValue108 = payload;
                Assert.That(assertedValue108, Is.TypeOf<PasswordRecoveryEmailPayload>());
                var actual = ((PasswordRecoveryEmailPayload)assertedValue108);
                Assert.That(actual.UserId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.User>("80808080-8080-8080-8080-808080808080")));
                Assert.That(actual.TokenId, Is.EqualTo(ParseTestId<LgymApi.Domain.Entities.PasswordResetToken>("90909090-9090-9090-9090-909090909090")));
                Assert.That(actual.UserName, Is.EqualTo("Golden Recovery"));
                Assert.That(actual.RecipientEmail, Is.EqualTo("golden.recovery@example.test"));
                Assert.That(actual.ResetToken, Is.EqualTo("golden-reset-token"));
                Assert.That(actual.ResetUrl, Is.EqualTo("https://golden-sentinel.example.test/reset"));
                Assert.That(actual.CultureName, Is.EqualTo(MalformedCultureName));
                AssertPayloadMetadata(actual, ParseTestId<LgymApi.Domain.ValueObjects.CorrelationScope>("90909090-9090-9090-9090-909090909090"), "user.password.recovery");
            })).SetName("Email payload golden matrix: Password recovery");
    }

    private static GoldenPayloadEvaluation EvaluateGoldenCase(EmailPayloadGoldenCase goldenCase)
    {
        var wire = JsonDocument.Parse(JsonSerializer.Serialize(goldenCase.Payload, goldenCase.Payload.GetType(), SharedSerializationOptions.Current));
        var roundtrippedPayload = goldenCase.Deserialize(wire.RootElement.GetRawText());
        var legacyPayload = goldenCase.Deserialize(goldenCase.LegacyJson);
        var nullWire = JsonDocument.Parse(JsonSerializer.Serialize(goldenCase.NullPayload, goldenCase.NullPayload.GetType(), SharedSerializationOptions.Current));

        return new GoldenPayloadEvaluation(wire, nullWire, roundtrippedPayload, legacyPayload);
    }

    private static void AssertCamelCaseProperties(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            Assert.That(element.TryGetProperty(propertyName, out _), Is.True);
            Assert.That(element.TryGetProperty(char.ToUpperInvariant(propertyName[0]) + propertyName[1..], out _), Is.False);
        }
    }

    public sealed record EmailPayloadGoldenCase(
        string Name,
        IEmailPayload Payload,
        IEmailPayload NullPayload,
        string NullPropertyName,
        string LegacyJson,
        Action<JsonElement> AssertWire,
        Action<IEmailPayload> AssertPayload)
    {
        public IEmailPayload Deserialize(string json)
        {
            return Payload switch
            {
                WelcomeEmailPayload => JsonSerializer.Deserialize<WelcomeEmailPayload>(json, SharedSerializationOptions.Current)!,
                InvitationEmailPayload => JsonSerializer.Deserialize<InvitationEmailPayload>(json, SharedSerializationOptions.Current)!,
                InvitationAcceptedEmailPayload => JsonSerializer.Deserialize<InvitationAcceptedEmailPayload>(json, SharedSerializationOptions.Current)!,
                InvitationRevokedEmailPayload => JsonSerializer.Deserialize<InvitationRevokedEmailPayload>(json, SharedSerializationOptions.Current)!,
                TrainingCompletedEmailPayload => JsonSerializer.Deserialize<TrainingCompletedEmailPayload>(json, SharedSerializationOptions.Current)!,
                PasswordRecoveryEmailPayload => JsonSerializer.Deserialize<PasswordRecoveryEmailPayload>(json, SharedSerializationOptions.Current)!,
                _ => throw new InvalidOperationException($"Unsupported golden payload type: {Payload.GetType().FullName}")
            };
        }
    }

    private sealed record GoldenPayloadEvaluation(
        JsonDocument Wire,
        JsonDocument NullWire,
        IEmailPayload RoundtrippedPayload,
        IEmailPayload LegacyPayload);

    private static (string Json, TPayload Payload) SerializeAndDeserialize<TPayload>(TPayload fixture)
    {
        var json = JsonSerializer.Serialize(fixture, SharedSerializationOptions.Current);
        var payload = JsonSerializer.Deserialize<TPayload>(json, SharedSerializationOptions.Current);

        Assert.That(payload, Is.Not.Null);
        return (json, payload!);
    }

    private static void AssertPayloadMetadata(
        IEmailPayload payload,
        Id<LgymApi.Domain.ValueObjects.CorrelationScope> expectedCorrelationId,
        string expectedNotificationType)
    {
        Assert.That(payload.CorrelationId, Is.EqualTo(expectedCorrelationId));
        Assert.That(payload.NotificationType.Value, Is.EqualTo(expectedNotificationType));
        Assert.That(payload.Culture.Name, Is.EqualTo("en-US"));
    }

    #endregion
}



