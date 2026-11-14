using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
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
        ownerLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerToken = (await ownerLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        using var recipientLogin = await Client.PostAsJsonAsync("/api/login", new { name = recipient.Name, password = "password123" });
        recipientLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var recipientToken = (await recipientLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var share = await Client.PostAsync($"/api/{plan.Id}/share", null);
        share.StatusCode.Should().Be(HttpStatusCode.OK);
        var code = (await share.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shareCode").GetString();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recipientToken);
        using var copy = await Client.PostAsJsonAsync("/api/copy", new { shareCode = code });
        copy.StatusCode.Should().Be(HttpStatusCode.Created);
        var copyId = (await copy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        copyId.Should().NotBe(plan.Id.ToString());
        Id<PlanDay> copiedDayId;
        Id<Exercise> copiedExerciseId;
        await using (var copiedScope = Factory.Services.CreateAsyncScope())
        {
            var db = copiedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var copiedPlan = (await db.Plans.AsNoTracking().ToListAsync()).Single(p => p.Id.ToString() == copyId);
            var copiedDay = await db.PlanDays.AsNoTracking().SingleAsync(d => d.PlanId == copiedPlan.Id);
            var copiedDefinition = await db.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == copiedDay.Id);
            copiedDay.Id.Should().NotBe(day.Id);
            copiedDefinition.Id.Should().NotBe(planExercise.Id);
            copiedDefinition.ExerciseId.Should().NotBe(exercise.Id);
            copiedDayId = copiedDay.Id;
            copiedExerciseId = copiedDefinition.ExerciseId;
        }
        using var denied = await Client.PostAsJsonAsync($"/api/{recipient.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "Intrusion" });
        denied.IsSuccessStatusCode.Should().BeFalse();
        using var editCopy = await Client.PostAsJsonAsync($"/api/{recipient.Id}/updatePlan", new { _id = copyId, name = "Copy edited" });
        editCopy.StatusCode.Should().Be(HttpStatusCode.OK);
        using var editCopyExercise = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = copiedExerciseId.ToString(), name = "Copy exercise edited", bodyPart = "Back" });
        editCopyExercise.StatusCode.Should().Be(HttpStatusCode.OK);
        using var editCopyDay = await Client.PostAsJsonAsync("/api/planDay/updatePlanDay", new
        {
            _id = copiedDayId.ToString(), name = "Copy day edited",
            exercises = new[] { new { exercise = copiedExerciseId.ToString(), series = 4, reps = "12" } }
        });
        editCopyDay.StatusCode.Should().Be(HttpStatusCode.OK);
        await using (var unchangedScope = Factory.Services.CreateAsyncScope())
        {
            var db = unchangedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var originalPlan = await db.Plans.AsNoTracking().SingleAsync(p => p.Id == plan.Id);
            originalPlan.UserId.Should().Be(owner.Id);
            originalPlan.Name.Should().Be("Original plan");
            originalPlan.Purpose.Should().Be(PlanPurpose.Personal);
            var originalDay = await db.PlanDays.AsNoTracking().SingleAsync(d => d.Id == day.Id);
            originalDay.PlanId.Should().Be(plan.Id);
            originalDay.Name.Should().Be("Original day");
            var originalDefinition = await db.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == day.Id);
            originalDefinition.Id.Should().Be(planExercise.Id);
            originalDefinition.ExerciseId.Should().Be(exercise.Id);
            originalDefinition.Series.Should().Be(3);
            originalDefinition.Reps.Should().Be("8");
            var originalExercise = await db.Exercises.AsNoTracking().SingleAsync(e => e.Id == exercise.Id);
            originalExercise.UserId.Should().Be(owner.Id);
            originalExercise.Name.Should().Be("Original exercise");
            originalExercise.BodyPart.Should().Be(BodyParts.Chest);
            var originalTraining = await db.Trainings.AsNoTracking().SingleAsync(t => t.Id == training.Id);
            originalTraining.UserId.Should().Be(owner.Id);
            originalTraining.TypePlanDayId.Should().Be(day.Id);
            originalTraining.GymId.Should().Be(gym.Id);
            originalTraining.CreatedAt.Should().Be(dayTime);
            var originalScore = await db.ExerciseScores.AsNoTracking().SingleAsync(s => s.Id == score.Id);
            originalScore.UserId.Should().Be(owner.Id);
            originalScore.TrainingId.Should().Be(training.Id);
            originalScore.ExerciseId.Should().Be(exercise.Id);
            originalScore.Reps.Should().Be(8);
            originalScore.Series.Should().Be(3);
            originalScore.WeightValue.Should().Be(80);
            originalScore.Unit.Should().Be(WeightUnits.Kilograms);
            var originalLink = await db.TrainingExerciseScores.AsNoTracking().SingleAsync(l => l.Id == link.Id);
            originalLink.TrainingId.Should().Be(training.Id);
            originalLink.ExerciseScoreId.Should().Be(score.Id);
        }
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var editSource = await Client.PostAsJsonAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "Source edited", purpose = "Personal" });
        editSource.StatusCode.Should().Be(HttpStatusCode.OK);
        using var editSourceExercise = await Client.PostAsJsonAsync("/api/exercise/updateExercise", new { _id = exercise.Id.ToString(), name = "Source exercise edited", bodyPart = "Chest" });
        editSourceExercise.StatusCode.Should().Be(HttpStatusCode.OK);
        using var editSourceDay = await Client.PostAsJsonAsync("/api/planDay/updatePlanDay", new
        {
            _id = day.Id.ToString(), name = "Source day edited",
            exercises = new[] { new { exercise = exercise.Id.ToString(), series = 5, reps = "6" } }
        });
        editSourceDay.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var verification = Factory.Services.CreateAsyncScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<AppDbContext>();
            var plans = await db.Plans.AsNoTracking().ToListAsync();
            plans.Single(p => p.Id == plan.Id).Name.Should().Be("Source edited");
            var copiedPlan = plans.Single(p => p.Id.ToString() == copyId);
            copiedPlan.UserId.Should().Be(recipient.Id);
            copiedPlan.Name.Should().Be("Copy edited");
            var copiedDay = await db.PlanDays.AsNoTracking().SingleAsync(d => d.PlanId == copiedPlan.Id);
            copiedDay.Id.Should().Be(copiedDayId);
            copiedDay.Name.Should().Be("Copy day edited");
            var copiedExercise = await db.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == copiedDay.Id);
            copiedExercise.ExerciseId.Should().Be(copiedExerciseId);
            copiedExercise.Series.Should().Be(4);
            copiedExercise.Reps.Should().Be("12");
            var copiedCatalogExercise = await db.Exercises.AsNoTracking().SingleAsync(e => e.Id == copiedExerciseId);
            copiedCatalogExercise.UserId.Should().Be(recipient.Id);
            copiedCatalogExercise.Name.Should().Be("Copy exercise edited");
            copiedCatalogExercise.BodyPart.Should().Be(BodyParts.Back);
            var persistedDay = await db.PlanDays.AsNoTracking().SingleAsync(d => d.Id == day.Id);
            persistedDay.PlanId.Should().Be(plan.Id);
            persistedDay.Name.Should().Be("Source day edited");
            var persistedExercise = await db.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == day.Id);
            persistedExercise.PlanDayId.Should().Be(day.Id);
            persistedExercise.ExerciseId.Should().Be(exercise.Id);
            persistedExercise.Series.Should().Be(5);
            persistedExercise.Reps.Should().Be("6");
            var sourceCatalogExercise = await db.Exercises.AsNoTracking().SingleAsync(e => e.Id == exercise.Id);
            sourceCatalogExercise.UserId.Should().Be(owner.Id);
            sourceCatalogExercise.Name.Should().Be("Source exercise edited");
            sourceCatalogExercise.BodyPart.Should().Be(BodyParts.Chest);
            var persistedTraining = await db.Trainings.AsNoTracking().SingleAsync(t => t.Id == training.Id);
            persistedTraining.UserId.Should().Be(owner.Id);
            persistedTraining.TypePlanDayId.Should().Be(day.Id);
            persistedTraining.GymId.Should().Be(gym.Id);
            persistedTraining.CreatedAt.Should().Be(dayTime);
            var persistedScore = await db.ExerciseScores.AsNoTracking().SingleAsync(s => s.Id == score.Id);
            persistedScore.UserId.Should().Be(owner.Id);
            persistedScore.TrainingId.Should().Be(training.Id);
            persistedScore.ExerciseId.Should().Be(exercise.Id);
            persistedScore.Reps.Should().Be(8);
            persistedScore.Series.Should().Be(3);
            persistedScore.WeightValue.Should().Be(80);
            persistedScore.Unit.Should().Be(WeightUnits.Kilograms);
            var persistedLink = await db.TrainingExerciseScores.AsNoTracking().SingleAsync(l => l.Id == link.Id);
            persistedLink.TrainingId.Should().Be(training.Id);
            persistedLink.ExerciseScoreId.Should().Be(score.Id);
        }

        using var history = await Client.PostAsJsonAsync($"/api/{owner.Id}/getTrainingByDate", new { createdAt = dayTime });
        history.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await history.Content.ReadFromJsonAsync<JsonElement>();
        var row = rows.EnumerateArray().Single();
        row.GetProperty("_id").GetString().Should().Be(training.Id.ToString());
        row.GetProperty("type").GetString().Should().Be(day.Id.ToString());
        row.GetProperty("planDay").GetProperty("name").GetString().Should().Be("Source day edited");
        var historyExercise = row.GetProperty("exercises").EnumerateArray().Single();
        historyExercise.GetProperty("exerciseScoreId").GetString().Should().Be(score.Id.ToString());
        historyExercise.GetProperty("exerciseDetails").GetProperty("_id").GetString().Should().Be(exercise.Id.ToString());
        historyExercise.GetProperty("scoresDetails").EnumerateArray().Single().GetProperty("weight").GetDouble().Should().Be(80);
    }
}
