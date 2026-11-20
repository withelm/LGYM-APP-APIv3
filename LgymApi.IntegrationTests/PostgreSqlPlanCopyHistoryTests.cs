using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
public sealed class PostgreSqlPlanCopyHistoryTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task CompletedHistoryRemainsAttachedAfterSourceAndCopyAreEdited()
    {
        var owner = await SeedUserAsync("copy-history-owner", "copy-history-owner@example.test");
        var recipient = await SeedUserAsync("copy-history-recipient", "copy-history-recipient@example.test");
        var dayTime = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Original plan", Purpose = PlanPurpose.Personal };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Original day" };
        var exercise = new Exercise { Id = Id<Exercise>.New(), UserId = owner.Id, Name = "Original exercise", BodyPart = BodyParts.Chest };
        var planExercise = new PlanDayExercise { Id = Id<PlanDayExercise>.New(), PlanDayId = day.Id, ExerciseId = exercise.Id, Series = 3, Reps = "8" };
        var gym = new Gym { Id = Id<Gym>.New(), UserId = owner.Id, Name = "History gym" };
        var training = new Training { Id = Id<Training>.New(), UserId = owner.Id, TypePlanDayId = day.Id, GymId = gym.Id, CreatedAt = dayTime };
        var score = new ExerciseScore { Id = Id<ExerciseScore>.New(), UserId = owner.Id, ExerciseId = exercise.Id, TrainingId = training.Id, Reps = 8, Series = 3, Weight = 80, Unit = WeightUnits.Kilograms, CreatedAt = dayTime };
        var link = new TrainingExerciseScore { Id = Id<TrainingExerciseScore>.New(), TrainingId = training.Id, ExerciseScoreId = score.Id };
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(plan, day, exercise, planExercise, gym, training, score, link);
            await db.SaveChangesAsync();
        }

        using var ownerLogin = await Client.PostAsJsonAsync("/api/login", new { name = owner.Name, password = "password123" });
        Assert.That(ownerLogin.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerToken = (await ownerLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        using var recipientLogin = await Client.PostAsJsonAsync("/api/login", new { name = recipient.Name, password = "password123" });
        Assert.That(recipientLogin.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var recipientToken = (await recipientLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var share = await Client.PostAsync($"/api/{plan.Id}/share", null);
        Assert.That(share.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var code = (await share.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shareCode").GetString();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recipientToken);
        using var copy = await Client.PostAsJsonAsync("/api/copy", new { shareCode = code });
        Assert.That(copy.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var copyId = (await copy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        Assert.That(copyId, Is.Not.EqualTo(plan.Id.ToString()));
        Id<PlanDay> copiedDayId;
        Id<Exercise> copiedExerciseId;
        await using (var copiedScope = Factory.Services.CreateAsyncScope())
        {
            var db = copiedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var copiedPlan = (await db.Plans.AsNoTracking().ToListAsync()).Single(p => p.Id.ToString() == copyId);
            var copiedDay = await db.PlanDays.AsNoTracking().SingleAsync(d => d.PlanId == copiedPlan.Id);
            var copiedDefinition = await db.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == copiedDay.Id);
            Assert.That(copiedDay.Id, Is.Not.EqualTo(day.Id));
            Assert.That(copiedDefinition.Id, Is.Not.EqualTo(planExercise.Id));
            Assert.That(copiedDefinition.ExerciseId, Is.Not.EqualTo(exercise.Id));
            copiedDayId = copiedDay.Id;
            copiedExerciseId = copiedDefinition.ExerciseId;
        }
        using var denied = await Client.PostAsJsonAsync($"/api/{recipient.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "Intrusion" });
        Assert.That(denied.IsSuccessStatusCode, Is.False);
        using var editCopy = await Client.PostAsJsonAsync($"/api/{recipient.Id}/updatePlan", new { _id = copyId, name = "Copy edited" });
        Assert.That(editCopy.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var editCopyExercise = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = copiedExerciseId.ToString(), name = "Copy exercise edited", bodyPart = "Back" });
        Assert.That(editCopyExercise.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var editCopyDay = await Client.PostAsJsonAsync("/api/planDay/updatePlanDay", new
        {
            _id = copiedDayId.ToString(), name = "Copy day edited",
            exercises = new[] { new { exercise = copiedExerciseId.ToString(), series = 4, reps = "12" } }
        });
        Assert.That(editCopyDay.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await using (var unchangedScope = Factory.Services.CreateAsyncScope())
        {
            var db = unchangedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var originalPlan = await db.Plans.AsNoTracking().SingleAsync(p => p.Id == plan.Id);
            Assert.That(originalPlan.UserId, Is.EqualTo(owner.Id));
            Assert.That(originalPlan.Name, Is.EqualTo("Original plan"));
            Assert.That(originalPlan.Purpose, Is.EqualTo(PlanPurpose.Personal));
            var originalDay = await db.PlanDays.AsNoTracking().SingleAsync(d => d.Id == day.Id);
            Assert.That(originalDay.PlanId, Is.EqualTo(plan.Id));
            Assert.That(originalDay.Name, Is.EqualTo("Original day"));
            var originalDefinition = await db.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == day.Id);
            Assert.That(originalDefinition.Id, Is.EqualTo(planExercise.Id));
            Assert.That(originalDefinition.ExerciseId, Is.EqualTo(exercise.Id));
            Assert.That(originalDefinition.Series, Is.EqualTo(3));
            Assert.That(originalDefinition.Reps, Is.EqualTo("8"));
            var originalExercise = await db.Exercises.AsNoTracking().SingleAsync(e => e.Id == exercise.Id);
            Assert.That(originalExercise.UserId, Is.EqualTo(owner.Id));
            Assert.That(originalExercise.Name, Is.EqualTo("Original exercise"));
            Assert.That(originalExercise.BodyPart, Is.EqualTo(BodyParts.Chest));
            var originalTraining = await db.Trainings.AsNoTracking().SingleAsync(t => t.Id == training.Id);
            Assert.That(originalTraining.UserId, Is.EqualTo(owner.Id));
            Assert.That(originalTraining.TypePlanDayId, Is.EqualTo(day.Id));
            Assert.That(originalTraining.GymId, Is.EqualTo(gym.Id));
            Assert.That(originalTraining.CreatedAt, Is.EqualTo(dayTime));
            var originalScore = await db.ExerciseScores.AsNoTracking().SingleAsync(s => s.Id == score.Id);
            Assert.That(originalScore.UserId, Is.EqualTo(owner.Id));
            Assert.That(originalScore.TrainingId, Is.EqualTo(training.Id));
            Assert.That(originalScore.ExerciseId, Is.EqualTo(exercise.Id));
            Assert.That(originalScore.Reps, Is.EqualTo(8));
            Assert.That(originalScore.Series, Is.EqualTo(3));
            Assert.That(originalScore.WeightValue, Is.EqualTo(80));
            Assert.That(originalScore.Unit, Is.EqualTo(WeightUnits.Kilograms));
            var originalLink = await db.TrainingExerciseScores.AsNoTracking().SingleAsync(l => l.Id == link.Id);
            Assert.That(originalLink.TrainingId, Is.EqualTo(training.Id));
            Assert.That(originalLink.ExerciseScoreId, Is.EqualTo(score.Id));
        }
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var editSource = await Client.PostAsJsonAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "Source edited", purpose = "Personal" });
        Assert.That(editSource.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var editSourceExercise = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = exercise.Id.ToString(), name = "Source exercise edited", bodyPart = "Chest" });
        Assert.That(editSourceExercise.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var editSourceDay = await Client.PostAsJsonAsync("/api/planDay/updatePlanDay", new
        {
            _id = day.Id.ToString(), name = "Source day edited",
            exercises = new[] { new { exercise = exercise.Id.ToString(), series = 5, reps = "6" } }
        });
        Assert.That(editSourceDay.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using (var verification = Factory.Services.CreateAsyncScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<AppDbContext>();
            var plans = await db.Plans.AsNoTracking().ToListAsync();
            Assert.That(plans.Single(p => p.Id == plan.Id).Name, Is.EqualTo("Source edited"));
            var copiedPlan = plans.Single(p => p.Id.ToString() == copyId);
            Assert.That(copiedPlan.UserId, Is.EqualTo(recipient.Id));
            Assert.That(copiedPlan.Name, Is.EqualTo("Copy edited"));
            var copiedDay = await db.PlanDays.AsNoTracking().SingleAsync(d => d.PlanId == copiedPlan.Id);
            Assert.That(copiedDay.Id, Is.EqualTo(copiedDayId));
            Assert.That(copiedDay.Name, Is.EqualTo("Copy day edited"));
            var copiedExercise = await db.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == copiedDay.Id);
            Assert.That(copiedExercise.ExerciseId, Is.EqualTo(copiedExerciseId));
            Assert.That(copiedExercise.Series, Is.EqualTo(4));
            Assert.That(copiedExercise.Reps, Is.EqualTo("12"));
            var copiedCatalogExercise = await db.Exercises.AsNoTracking().SingleAsync(e => e.Id == copiedExerciseId);
            Assert.That(copiedCatalogExercise.UserId, Is.EqualTo(recipient.Id));
            Assert.That(copiedCatalogExercise.Name, Is.EqualTo("Copy exercise edited"));
            Assert.That(copiedCatalogExercise.BodyPart, Is.EqualTo(BodyParts.Back));
            var persistedDay = await db.PlanDays.AsNoTracking().SingleAsync(d => d.Id == day.Id);
            Assert.That(persistedDay.PlanId, Is.EqualTo(plan.Id));
            Assert.That(persistedDay.Name, Is.EqualTo("Source day edited"));
            var persistedExercise = await db.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == day.Id);
            Assert.That(persistedExercise.PlanDayId, Is.EqualTo(day.Id));
            Assert.That(persistedExercise.ExerciseId, Is.EqualTo(exercise.Id));
            Assert.That(persistedExercise.Series, Is.EqualTo(5));
            Assert.That(persistedExercise.Reps, Is.EqualTo("6"));
            var sourceCatalogExercise = await db.Exercises.AsNoTracking().SingleAsync(e => e.Id == exercise.Id);
            Assert.That(sourceCatalogExercise.UserId, Is.EqualTo(owner.Id));
            Assert.That(sourceCatalogExercise.Name, Is.EqualTo("Source exercise edited"));
            Assert.That(sourceCatalogExercise.BodyPart, Is.EqualTo(BodyParts.Chest));
            var persistedTraining = await db.Trainings.AsNoTracking().SingleAsync(t => t.Id == training.Id);
            Assert.That(persistedTraining.UserId, Is.EqualTo(owner.Id));
            Assert.That(persistedTraining.TypePlanDayId, Is.EqualTo(day.Id));
            Assert.That(persistedTraining.GymId, Is.EqualTo(gym.Id));
            Assert.That(persistedTraining.CreatedAt, Is.EqualTo(dayTime));
            var persistedScore = await db.ExerciseScores.AsNoTracking().SingleAsync(s => s.Id == score.Id);
            Assert.That(persistedScore.UserId, Is.EqualTo(owner.Id));
            Assert.That(persistedScore.TrainingId, Is.EqualTo(training.Id));
            Assert.That(persistedScore.ExerciseId, Is.EqualTo(exercise.Id));
            Assert.That(persistedScore.Reps, Is.EqualTo(8));
            Assert.That(persistedScore.Series, Is.EqualTo(3));
            Assert.That(persistedScore.WeightValue, Is.EqualTo(80));
            Assert.That(persistedScore.Unit, Is.EqualTo(WeightUnits.Kilograms));
            var persistedLink = await db.TrainingExerciseScores.AsNoTracking().SingleAsync(l => l.Id == link.Id);
            Assert.That(persistedLink.TrainingId, Is.EqualTo(training.Id));
            Assert.That(persistedLink.ExerciseScoreId, Is.EqualTo(score.Id));
        }

        using var history = await Client.PostAsJsonAsync($"/api/{owner.Id}/getTrainingByDate", new { createdAt = dayTime });
        Assert.That(history.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rows = await history.Content.ReadFromJsonAsync<JsonElement>();
        var row = rows.EnumerateArray().Single();
        Assert.That(row.GetProperty("_id").GetString(), Is.EqualTo(training.Id.ToString()));
        Assert.That(row.GetProperty("type").GetString(), Is.EqualTo(day.Id.ToString()));
        Assert.That(row.GetProperty("planDay").GetProperty("name").GetString(), Is.EqualTo("Source day edited"));
        var historyExercise = row.GetProperty("exercises").EnumerateArray().Single();
        Assert.That(historyExercise.GetProperty("exerciseScoreId").GetString(), Is.EqualTo(score.Id.ToString()));
        Assert.That(historyExercise.GetProperty("exerciseDetails").GetProperty("_id").GetString(), Is.EqualTo(exercise.Id.ToString()));
        Assert.That(historyExercise.GetProperty("scoresDetails").EnumerateArray().Single().GetProperty("weight").GetDouble(), Is.EqualTo(80));
    }
}
