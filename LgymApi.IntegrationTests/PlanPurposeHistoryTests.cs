using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PlanPurposeHistoryTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Day = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task CoachSeesWorkoutResultsButOnlyTraineePersonalPlanMetadata_OwnerSeesAllHistory()
    {
        var trainee = await SeedUserAsync("history-owner", "history-owner@example.test");
        var coach = await SeedUserAsync("history-coach", "history-coach@example.test");
        var foreign = await SeedUserAsync("history-foreign", "history-foreign@example.test");
        var plans = new[]
        {
            new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Personal", Purpose = PlanPurpose.Personal },
            new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Professional", Purpose = PlanPurpose.Professional },
            new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Unclassified", Purpose = PlanPurpose.Unclassified },
            new Plan { Id = Id<Plan>.New(), UserId = foreign.Id, Name = "Foreign", Purpose = PlanPurpose.Personal }
        };
        var days = plans.Select(plan => new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = plan.Name + " day" }).ToArray();
        var gym = new Gym { Id = Id<Gym>.New(), UserId = trainee.Id, Name = "History gym" };
        var exercise = new Exercise { Id = Id<Exercise>.New(), Name = "History exercise", BodyPart = BodyParts.Chest };
        var trainings = days.Select(day => new Training { Id = Id<Training>.New(), UserId = trainee.Id, TypePlanDayId = day.Id, GymId = gym.Id, CreatedAt = Day }).ToArray();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = trainee.Id });
            db.Plans.AddRange(plans);
            db.PlanDays.AddRange(days);
            db.Gyms.Add(gym);
            db.Exercises.Add(exercise);
            db.Trainings.AddRange(trainings);
            foreach (var training in trainings)
            {
                var score = new ExerciseScore { Id = Id<ExerciseScore>.New(), UserId = trainee.Id, ExerciseId = exercise.Id, TrainingId = training.Id, Reps = 8, Series = 1, Weight = 80, Unit = WeightUnits.Kilograms, CreatedAt = Day };
                db.ExerciseScores.Add(score);
                db.TrainingExerciseScores.Add(new TrainingExerciseScore { Id = Id<TrainingExerciseScore>.New(), TrainingId = training.Id, ExerciseScoreId = score.Id });
            }
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        var ownerRows = await ReadHistoryAsync($"/api/{trainee.Id}/getTrainingByDate");
        SetAuthorizationHeader(coach.Id);
        var coachRows = await ReadHistoryAsync($"/api/trainer/trainees/{trainee.Id}/trainings/by-date");

        Assert.That(ownerRows.Length, Is.EqualTo(4));
        Assert.That(coachRows.Length, Is.EqualTo(4));
        for (var i = 0; i < trainings.Length; i++)
        {
            var ownerRow = ownerRows.Single(row => row.GetProperty("_id").GetString() == trainings[i].Id.ToString());
            var coachRow = coachRows.Single(row => row.GetProperty("_id").GetString() == trainings[i].Id.ToString());
            Assert.That(ownerRow.GetProperty("type").GetString(), Is.EqualTo(days[i].Id.ToString()));
            Assert.That(ownerRow.GetProperty("planDay").GetProperty("name").GetString(), Is.EqualTo(i == 3 ? string.Empty : days[i].Name));
            Assert.That(coachRow.GetProperty("gym").GetString(), Is.EqualTo(gym.Name));
            Assert.That(coachRow.GetProperty("exercises").GetArrayLength(), Is.EqualTo(1));
            Assert.That(coachRow.GetProperty("exercises")[0].GetProperty("scoresDetails")[0].GetProperty("weight").GetDouble(), Is.EqualTo(80));
            Assert.That(coachRow.GetProperty("type").GetString(), Is.EqualTo(i == 0 ? days[i].Id.ToString() : string.Empty));
            Assert.That(coachRow.GetProperty("planDay").GetProperty("_id").GetString(), Is.EqualTo(i == 0 ? days[i].Id.ToString() : string.Empty));
            Assert.That(coachRow.GetProperty("planDay").GetProperty("name").GetString(), Is.EqualTo(i == 0 ? days[i].Name : string.Empty));
        }
    }

    [Test]
    public async Task OwnerReclassificationChangesCoachMetadataForExistingWorkoutWithoutChangingOwnerHistory()
    {
        var trainee = await SeedUserAsync("reclass-owner", "reclass-owner@example.test");
        var coach = await SeedUserAsync("reclass-coach", "reclass-coach@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Reclassified", Purpose = PlanPurpose.Personal };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Existing day" };
        var gym = new Gym { Id = Id<Gym>.New(), UserId = trainee.Id, Name = "Gym" };
        var training = new Training { Id = Id<Training>.New(), UserId = trainee.Id, TypePlanDayId = day.Id, GymId = gym.Id, CreatedAt = Day };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = trainee.Id });
            db.Plans.Add(plan);
            db.PlanDays.Add(day);
            db.Gyms.Add(gym);
            db.Trainings.Add(training);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(coach.Id);
        var before = await ReadHistoryAsync($"/api/trainer/trainees/{trainee.Id}/trainings/by-date");
        Assert.That(before.Single().GetProperty("type").GetString(), Is.EqualTo(day.Id.ToString()));
        SetAuthorizationHeader(trainee.Id);
        using var update = await PostAsJsonWithApiOptionsAsync($"/api/{trainee.Id}/updatePlan", new { _id = plan.Id.ToString(), name = plan.Name, purpose = "Professional" });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var owner = await ReadHistoryAsync($"/api/{trainee.Id}/getTrainingByDate");
        SetAuthorizationHeader(coach.Id);
        var after = await ReadHistoryAsync($"/api/trainer/trainees/{trainee.Id}/trainings/by-date");

        Assert.That(owner.Single().GetProperty("planDay").GetProperty("name").GetString(), Is.EqualTo(day.Name));
        Assert.That(after.Single().GetProperty("_id").GetString(), Is.EqualTo(training.Id.ToString()));
        Assert.That(after.Single().GetProperty("type").GetString(), Is.Empty);
        Assert.That(after.Single().GetProperty("planDay").GetProperty("name").GetString(), Is.Empty);
    }

    private async Task<JsonElement[]> ReadHistoryAsync(string route)
    {
        using var response = await Client.PostAsJsonAsync(route, new { createdAt = Day });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().Select(row => row.Clone()).ToArray();
    }
}
