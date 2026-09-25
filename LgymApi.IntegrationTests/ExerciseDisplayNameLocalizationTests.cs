using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ExerciseDisplayNameLocalizationTests : IntegrationTestBase
{
    [Test]
    public async Task ExerciseReads_LocalizeOnlyDisplayNameAcrossUserAndTrainerSurfaces()
    {
        var trainee = await SeedUserAsync(
            name: "localized-exercise-trainee",
            email: "localized-exercise-trainee@example.com");
        var trainer = await SeedUserAsync(
            name: "localized-exercise-trainer",
            email: "localized-exercise-trainer@example.com");
        var scenario = await SeedScenarioAsync(trainee.Id, trainer.Id);

        SetAuthorizationHeader(trainee.Id);
        SetAcceptLanguage("pl-PL");

        using (var globalResponse = await Client.GetAsync($"/api/exercise/{scenario.GlobalExerciseId}/getExercise"))
        {
            globalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await globalResponse.Content.ReadAsStringAsync());
            AssertNames(body.RootElement, "Bench Press", "Wyciskanie na lawce");
        }

        using (var customResponse = await Client.GetAsync($"/api/exercise/{scenario.CustomExerciseId}/getExercise"))
        {
            customResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await customResponse.Content.ReadAsStringAsync());
            AssertNames(body.RootElement, "Custom Row", "Custom Row");
        }

        using (var trainingResponse = await Client.PostAsJsonAsync(
                   $"/api/{trainee.Id}/getTrainingByDate",
                   new { createdAt = scenario.TrainingDate }))
        {
            trainingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await trainingResponse.Content.ReadAsStringAsync());
            var exercises = body.RootElement[0].GetProperty("exercises");
            AssertNames(FindExerciseDetails(exercises, scenario.GlobalExerciseId), "Bench Press", "Wyciskanie na lawce");
            AssertNames(FindExerciseDetails(exercises, scenario.CustomExerciseId), "Custom Row", "Custom Row");
        }

        using (var recordsResponse = await Client.GetAsync($"/api/mainRecords/{trainee.Id}/getLastMainRecords"))
        {
            recordsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await recordsResponse.Content.ReadAsStringAsync());
            AssertNames(FindExerciseDetails(body.RootElement, scenario.GlobalExerciseId), "Bench Press", "Wyciskanie na lawce");
            AssertNames(FindExerciseDetails(body.RootElement, scenario.CustomExerciseId), "Custom Row", "Custom Row");
        }

        SetAcceptLanguage("pl;q=0.1, en;q=0.9");
        using (var fallbackResponse = await Client.GetAsync($"/api/exercise/{scenario.GlobalExerciseId}/getExercise"))
        {
            fallbackResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await fallbackResponse.Content.ReadAsStringAsync());
            AssertNames(body.RootElement, "Bench Press", "Bench Press");
        }

        SetAuthorizationHeader(trainer.Id);
        SetAcceptLanguage("pl-PL");
        using var trainerResponse = await Client.PostAsJsonAsync(
            $"/api/trainer/trainees/{trainee.Id}/trainings/by-date",
            new { createdAt = scenario.TrainingDate });
        trainerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var trainerBody = JsonDocument.Parse(await trainerResponse.Content.ReadAsStringAsync());
        var trainerExercises = trainerBody.RootElement[0].GetProperty("exercises");
        AssertNames(FindExerciseDetails(trainerExercises, scenario.GlobalExerciseId), "Bench Press", "Wyciskanie na lawce");
        AssertNames(FindExerciseDetails(trainerExercises, scenario.CustomExerciseId), "Custom Row", "Custom Row");
    }

    private async Task<Scenario> SeedScenarioAsync(Id<User> traineeId, Id<User> trainerId)
    {
        var trainingDate = DateTimeOffset.UtcNow.AddDays(-1);
        var globalExercise = new Exercise
        {
            Id = Id<Exercise>.New(),
            Name = "Bench Press",
            BodyPart = BodyParts.Chest
        };
        var customExercise = new Exercise
        {
            Id = Id<Exercise>.New(),
            UserId = traineeId,
            Name = "Custom Row",
            BodyPart = BodyParts.Back
        };
        var plan = new Plan { Id = Id<Plan>.New(), UserId = traineeId, Name = "Localized plan" };
        var planDay = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Localized day" };
        var gym = new Gym { Id = Id<Gym>.New(), UserId = traineeId, Name = "Localized gym" };
        var training = new Training
        {
            Id = Id<Training>.New(),
            UserId = traineeId,
            TypePlanDayId = planDay.Id,
            GymId = gym.Id,
            CreatedAt = trainingDate
        };

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(new UserRole
        {
            UserId = trainerId,
            RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId
        });
        db.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId
        });
        db.Exercises.AddRange(globalExercise, customExercise);
        db.ExerciseTranslations.Add(new ExerciseTranslation
        {
            Id = Id<ExerciseTranslation>.New(),
            ExerciseId = globalExercise.Id,
            Culture = "pl",
            Name = "Wyciskanie na lawce"
        });
        db.Plans.Add(plan);
        db.PlanDays.Add(planDay);
        db.Gyms.Add(gym);
        db.Trainings.Add(training);

        AddExerciseResult(db, training, globalExercise, traineeId, trainingDate, 80, 0);
        AddExerciseResult(db, training, customExercise, traineeId, trainingDate, 60, 1);
        AddMainRecord(db, globalExercise.Id, traineeId, trainingDate, 80);
        AddMainRecord(db, customExercise.Id, traineeId, trainingDate, 60);
        await db.SaveChangesAsync();

        return new Scenario(globalExercise.Id, customExercise.Id, trainingDate.UtcDateTime);
    }

    private static void AddExerciseResult(
        AppDbContext db,
        Training training,
        Exercise exercise,
        Id<User> traineeId,
        DateTimeOffset createdAt,
        double weight,
        int order)
    {
        var score = new ExerciseScore
        {
            Id = Id<ExerciseScore>.New(),
            ExerciseId = exercise.Id,
            UserId = traineeId,
            Reps = 8,
            Series = 1,
            Weight = weight,
            Unit = WeightUnits.Kilograms,
            TrainingId = training.Id,
            CreatedAt = createdAt,
            Order = order
        };
        db.ExerciseScores.Add(score);
        db.TrainingExerciseScores.Add(new TrainingExerciseScore
        {
            Id = Id<TrainingExerciseScore>.New(),
            TrainingId = training.Id,
            ExerciseScoreId = score.Id,
            Order = order
        });
    }

    private static void AddMainRecord(
        AppDbContext db,
        Id<Exercise> exerciseId,
        Id<User> traineeId,
        DateTimeOffset date,
        double weight)
    {
        db.MainRecords.Add(new MainRecord
        {
            Id = Id<MainRecord>.New(),
            UserId = traineeId,
            ExerciseId = exerciseId,
            Weight = weight,
            Unit = WeightUnits.Kilograms,
            Date = date
        });
    }

    private void SetAcceptLanguage(string value)
    {
        Client.DefaultRequestHeaders.AcceptLanguage.Clear();
        Client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(value);
    }

    private static JsonElement FindExerciseDetails(JsonElement items, Id<Exercise> exerciseId)
    {
        foreach (var item in items.EnumerateArray())
        {
            var details = item.GetProperty("exerciseDetails");
            if (details.GetProperty("_id").GetString() == exerciseId.ToString())
            {
                return details;
            }
        }

        throw new InvalidOperationException($"Exercise {exerciseId} was not returned.");
    }

    private static void AssertNames(
        JsonElement exercise,
        string expectedName,
        string expectedDisplayName)
    {
        exercise.GetProperty("name").GetString().Should().Be(expectedName);
        exercise.GetProperty("displayName").GetString().Should().Be(expectedDisplayName);
    }

    private sealed record Scenario(
        Id<Exercise> GlobalExerciseId,
        Id<Exercise> CustomExerciseId,
        DateTime TrainingDate);
}
