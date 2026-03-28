using LgymApi.Domain.ValueObjects;
using System.Text.Json;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Common.Serialization;
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
    #region WelcomeEmailPayload Tests

    [Test]
    public void WelcomeEmailPayload_RoundtripsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var originalPayload = new WelcomeEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            UserName = "John Doe",
            RecipientEmail = "john@example.com",
            CultureName = "en-US"
        };
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<WelcomeEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That((Guid)deserialized!.UserId, Is.EqualTo(userId));
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
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)Guid.NewGuid(),
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
        var userId = Guid.NewGuid();
        var legacyJson = "{\"UserId\":\"" + userId.ToString() + "\"," +
                         "\"UserName\":\"Legacy User\"," +
                         "\"RecipientEmail\":\"legacy@example.com\"," +
                         "\"CultureName\":\"de-DE\"}";

        // Act
        var deserialized = JsonSerializer.Deserialize<WelcomeEmailPayload>(legacyJson, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That((Guid)deserialized!.UserId, Is.EqualTo(userId));
        Assert.That(deserialized.UserName, Is.EqualTo("Legacy User"));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("legacy@example.com"));
        Assert.That(deserialized.CultureName, Is.EqualTo("de-DE"));
    }

    [Test]
    public void WelcomeEmailPayload_RoundtripsPreservesAllFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var originalPayload = new WelcomeEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            UserName = "Test User",
            RecipientEmail = "test@example.com",
            CultureName = "es-ES"
        };
        var json = JsonSerializer.Serialize(originalPayload, SharedSerializationOptions.Current);

        // Act
        var deserialized = JsonSerializer.Deserialize<WelcomeEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert - each field must match exactly
        Assert.That(deserialized, Is.Not.Null);
        Assert.That((Guid)deserialized!.UserId, Is.EqualTo(userId), "UserId not preserved");
        Assert.That(deserialized.UserName, Is.EqualTo("Test User"), "UserName not preserved");
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("test@example.com"), "RecipientEmail not preserved");
        Assert.That(deserialized.CultureName, Is.EqualTo("es-ES"), "CultureName not preserved");
    }

    [Test]
    public void WelcomeEmailPayload_CorrelationIdDerivesFromUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var payload = new WelcomeEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            UserName = "Corr User",
            RecipientEmail = "corr@example.com",
            CultureName = "fr-FR"
        };

        // Act
        var json = JsonSerializer.Serialize(payload, SharedSerializationOptions.Current);
        var deserialized = JsonSerializer.Deserialize<WelcomeEmailPayload>(json, SharedSerializationOptions.Current);

        // Assert
        Assert.That(deserialized, Is.Not.Null);
        Assert.That((Guid)deserialized!.CorrelationId, Is.EqualTo(userId));
    }

    #endregion

    #region InvitationEmailPayload Tests

    [Test]
    public void InvitationEmailPayload_RoundtripsSuccessfully()
    {
        // Arrange
        var invitationId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var originalPayload = new InvitationEmailPayload
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId,
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
        Assert.That((Guid)deserialized!.InvitationId, Is.EqualTo(invitationId));
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
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)Guid.NewGuid(),
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
        var invitationId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(14);
        var legacyJson = "{\"InvitationId\":\"" + invitationId.ToString() + "\"," +
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
        Assert.That((Guid)deserialized!.InvitationId, Is.EqualTo(invitationId));
        Assert.That(deserialized.InvitationCode, Is.EqualTo("LEGACY123"));
        Assert.That(deserialized.TrainerName, Is.EqualTo("Old Trainer"));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("oldclient@example.com"));
    }

    [Test]
    public void InvitationEmailPayload_RoundtripsPreservesAllFields()
    {
        // Arrange
        var invitationId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        var originalPayload = new InvitationEmailPayload
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId,
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
        Assert.That((Guid)deserialized!.InvitationId, Is.EqualTo(invitationId), "InvitationId not preserved");
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
        var invitationId = Guid.NewGuid();
        var payload = new InvitationEmailPayload
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId,
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
        Assert.That((Guid)deserialized!.CorrelationId, Is.EqualTo(invitationId));
    }

    #endregion

    #region TrainingCompletedEmailPayload Tests

    [Test]
    public void TrainingCompletedEmailPayload_RoundtripsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;
        var originalPayload = new TrainingCompletedEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId,
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That((Guid)deserialized!.UserId, Is.EqualTo(userId));
        Assert.That((Guid)deserialized.TrainingId, Is.EqualTo(trainingId));
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
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var originalPayload = new TrainingCompletedEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId,
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Exercises, Is.Not.Null);
        Assert.That(deserialized.Exercises.Count, Is.EqualTo(2));

        // Verify first exercise
        var firstExercise = deserialized.Exercises.First();
        Assert.Multiple(() =>
        {
            Assert.That(firstExercise.ExerciseId, Is.EqualTo("ex1"));
            Assert.That(firstExercise.ExerciseName, Is.EqualTo("Bench Press"));
            Assert.That(firstExercise.Series, Is.EqualTo(1));
            Assert.That(firstExercise.Reps, Is.EqualTo(8));
            Assert.That(firstExercise.Weight, Is.EqualTo(80));
            Assert.That(firstExercise.Unit, Is.EqualTo(WeightUnits.Kilograms));
        });

        // Verify second exercise
        var secondExercise = deserialized.Exercises.Skip(1).First();
        Assert.Multiple(() =>
        {
            Assert.That(secondExercise.ExerciseId, Is.EqualTo("ex2"));
            Assert.That(secondExercise.ExerciseName, Is.EqualTo("Squat"));
            Assert.That(secondExercise.Series, Is.EqualTo(2));
            Assert.That(secondExercise.Reps, Is.EqualTo(6));
            Assert.That(secondExercise.Weight, Is.EqualTo(120));
            Assert.That(secondExercise.Unit, Is.EqualTo(WeightUnits.Kilograms));
        });
    }

    [Test]
    public void TrainingCompletedEmailPayload_SerializedJsonUsesCamelCase()
    {
        // Arrange
        var originalPayload = new TrainingCompletedEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)Guid.NewGuid(),
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)Guid.NewGuid(),
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
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)Guid.NewGuid(),
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)Guid.NewGuid(),
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
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;
        var legacyJson = "{\"UserId\":\"" + userId.ToString() + "\"," +
                         "\"TrainingId\":\"" + trainingId.ToString() + "\"," +
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That((Guid)deserialized!.UserId, Is.EqualTo(userId));
        Assert.That((Guid)deserialized.TrainingId, Is.EqualTo(trainingId));
        Assert.That(deserialized.RecipientEmail, Is.EqualTo("legacy@example.com"));
        Assert.That(deserialized.Exercises.Count, Is.EqualTo(1));
        Assert.That(deserialized.Exercises.First().ExerciseName, Is.EqualTo("Legacy Exercise"));
    }

    [Test]
    public void TrainingCompletedEmailPayload_DeserializesFromLegacyIntegerEnumValues()
    {
        // Arrange - simulate very old persisted payload with integer enum values (numeric 1 for Kilograms)
        // This represents payloads serialized before string enum enforcement
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;
        var legacyJson = "{\"UserId\":\"" + userId.ToString() + "\"," +
                         "\"TrainingId\":\"" + trainingId.ToString() + "\"," +
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That((Guid)deserialized!.UserId, Is.EqualTo(userId));
        Assert.That((Guid)deserialized.TrainingId, Is.EqualTo(trainingId));
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
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;
        var originalPayload = new TrainingCompletedEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId,
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
        Assert.That(deserialized, Is.Not.Null);
        Assert.That((Guid)deserialized!.UserId, Is.EqualTo(userId), "UserId not preserved");
        Assert.That((Guid)deserialized.TrainingId, Is.EqualTo(trainingId), "TrainingId not preserved");
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
        var trainingId = Guid.NewGuid();
        var payload = new TrainingCompletedEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)Guid.NewGuid(),
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId,
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
        Assert.That((Guid)deserialized!.CorrelationId, Is.EqualTo(trainingId));
    }

    #endregion

    #region Composer Compatibility Tests

    [Test]
    public void WelcomeEmailPayload_CanBeDeserializedByComposerConsumer()
    {
        // Arrange - simulate payload stored by EmailSchedulerService and read back by composer
        var userId = Guid.NewGuid();
        var storedPayload = new WelcomeEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
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
        var invitationId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var storedPayload = new InvitationEmailPayload
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId,
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
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;
        var storedPayload = new TrainingCompletedEmailPayload
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId,
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
        Assert.That(deserializedPayload, Is.Not.Null);
        Assert.That(deserializedPayload!.PlanDayName, Is.EqualTo("Composer Training"));
        Assert.That(deserializedPayload.Exercises.Count, Is.EqualTo(1));
        Assert.That(deserializedPayload.Exercises.First().ExerciseName, Is.EqualTo("Composer Exercise"));
    }

    #endregion
}
