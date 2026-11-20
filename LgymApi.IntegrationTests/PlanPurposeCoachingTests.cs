using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PlanPurposeCoachingTests : IntegrationTestBase
{
    [Test]
    public async Task CurrentCoachUpdatesAndAssignsTraineePersonalPlanThroughHttp()
    {
        var coach = await SeedUserAsync("managed-coach", "managed-coach@example.test");
        var owner = await SeedUserAsync("managed-owner", "managed-owner@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Before", Purpose = PlanPurpose.Personal, IsActive = false };
        var previous = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Previous", Purpose = PlanPurpose.Personal, IsActive = true };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id });
            db.Plans.AddRange(plan, previous);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(coach.Id);

        using var update = await Client.PostAsJsonAsync($"/api/trainer/trainees/{owner.Id}/plans/{plan.Id}/update", new { name = "After" });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var assign = await Client.PostAsync($"/api/trainer/trainees/{owner.Id}/plans/{plan.Id}/assign", null);
        Assert.That(assign.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await dbVerify.Plans.AsNoTracking().SingleAsync(item => item.Id == plan.Id);
        Assert.That(persisted.UserId, Is.EqualTo(owner.Id));
        Assert.That(persisted.Name, Is.EqualTo("After"));
        Assert.That(persisted.Purpose, Is.EqualTo(PlanPurpose.Personal));
        Assert.That(persisted.IsActive, Is.True);
        var previousPersisted = await dbVerify.Plans.AsNoTracking().SingleAsync(item => item.Id == previous.Id);
        Assert.That(previousPersisted.IsActive, Is.False);
    }

    [TestCase("update")]
    [TestCase("delete")]
    [TestCase("assign")]
    public async Task CurrentCoachCannotMutateForeignPersonalPlanByKnownId(string operation)
    {
        var coach = await SeedUserAsync("foreign-coach", "foreign-coach@example.test");
        var trainee = await SeedUserAsync("foreign-trainee", "foreign-trainee@example.test");
        var owner = await SeedUserAsync("foreign-plan-owner", "foreign-plan-owner@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Foreign", Purpose = PlanPurpose.Personal, IsActive = false };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = trainee.Id });
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(coach.Id);

        using var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/plans/{plan.Id}/{operation}", new { name = "Intrusion" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var verification = Factory.Services.CreateScope();
        var persisted = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(item => item.Id == plan.Id);
        Assert.That(persisted.UserId, Is.EqualTo(owner.Id));
        Assert.That(persisted.Name, Is.EqualTo("Foreign"));
        Assert.That(persisted.Purpose, Is.EqualTo(PlanPurpose.Personal));
        Assert.That(persisted.IsDeleted, Is.False);
        Assert.That(persisted.IsActive, Is.False);
    }

    [Test]
    public async Task CoachOfTrainer_ListsOnlyPersonalPlansAndCannotReachIndirectTrainee()
    {
        var coach = await SeedUserAsync("coach-a", "coach-a@example.test");
        var owner = await SeedUserAsync("coach-b", "coach-b@example.test");
        var trainee = await SeedUserAsync("trainee-c", "trainee-c@example.test");
        var personal = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Personal", Purpose = PlanPurpose.Personal };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.AddRange(TestDataFactory.ApprovedTrainerRole(db, coach.Id), TestDataFactory.ApprovedTrainerRole(db, owner.Id));
            db.TrainerTraineeLinks.AddRange(
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id },
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = owner.Id, TraineeId = trainee.Id });
            db.Plans.AddRange(personal,
                new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Library", Purpose = PlanPurpose.Professional },
                new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Legacy" },
                new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Indirect", Purpose = PlanPurpose.Personal });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(coach.Id);

        var response = await Client.GetAsync($"/api/trainer/trainees/{owner.Id}/plans");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(json.RootElement.EnumerateArray(), Has.Exactly(1).Items);
        Assert.That(json.RootElement[0].GetProperty("name").GetString(), Is.EqualTo("Personal"));
        var indirect = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/plans");
        Assert.That(indirect.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase(PlanPurpose.Professional, "update")]
    [TestCase(PlanPurpose.Unclassified, "update")]
    [TestCase(PlanPurpose.Professional, "delete")]
    [TestCase(PlanPurpose.Unclassified, "delete")]
    [TestCase(PlanPurpose.Professional, "assign")]
    [TestCase(PlanPurpose.Unclassified, "assign")]
    public async Task CoachCannotMutateNonPersonalPlanByKnownId(PlanPurpose purpose, string operation)
    {
        var coach = await SeedUserAsync("purpose-coach", "purpose-coach@example.test");
        var owner = await SeedUserAsync("purpose-owner", "purpose-owner@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Private", Purpose = purpose, IsActive = false };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.AddRange(TestDataFactory.ApprovedTrainerRole(db, coach.Id), TestDataFactory.ApprovedTrainerRole(db, owner.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id });
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(coach.Id);

        var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{owner.Id}/plans/{plan.Id}/{operation}", new { name = "Intrusion" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var verification = Factory.Services.CreateScope();
        var persisted = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.IgnoreQueryFilters().SingleAsync(item => item.Id == plan.Id);
        Assert.That(persisted.Name, Is.EqualTo("Private"));
        Assert.That(persisted.Purpose, Is.EqualTo(purpose));
        Assert.That(persisted.IsDeleted, Is.False);
        Assert.That(persisted.IsActive, Is.False);
    }

    [Test]
    public async Task CreatingProfessionalPlanDoesNotReplacePersonalActivePlan()
    {
        var owner = await SeedUserAsync();
        var active = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Active", Purpose = PlanPurpose.Personal, IsActive = true };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(active);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(owner.Id);

        var created = await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/createPlan", new { name = "Library", purpose = "Professional" });

        Assert.That(created.IsSuccessStatusCode, Is.True);
        using var verification = Factory.Services.CreateScope();
        var plans = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.AsNoTracking().ToListAsync();
        Assert.That(plans.Single(item => item.Name == "Library").IsActive, Is.False);
        Assert.That(plans.Single(item => item.Id == active.Id).IsActive, Is.True);
    }

    [Test]
    public async Task KnownForeignProfessionalDayCannotBeImportedThroughWorkoutCompletion()
    {
        var owner = await SeedUserAsync("source-owner", "source-owner@example.test");
        var actor = await SeedUserAsync("workout-actor", "workout-actor@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Secret", Purpose = PlanPurpose.Professional };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Secret day" };
        var gym = new Gym { Id = Id<Gym>.New(), UserId = actor.Id, Name = "Own gym" };
        var exercise = new Exercise { Id = Id<Exercise>.New(), UserId = actor.Id, Name = "Own exercise", BodyPart = BodyParts.Chest };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(plan, day, gym, exercise);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(actor.Id);

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{actor.Id}/addTraining", new
        {
            gym = gym.Id.ToString(), type = day.Id.ToString(), createdAt = DateTime.UtcNow,
            exercises = new[] { new { exercise = exercise.Id.ToString(), series = 1, reps = 8, weight = 50.0, unit = "Kilograms" } }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var verification = Factory.Services.CreateScope();
        Assert.That((await verification.ServiceProvider.GetRequiredService<AppDbContext>().Trainings.AnyAsync(t => t.UserId == actor.Id)), Is.False);
    }

    [TestCase(PlanPurpose.Professional, true)]
    [TestCase(PlanPurpose.Professional, false)]
    [TestCase(PlanPurpose.Unclassified, true)]
    [TestCase(PlanPurpose.Unclassified, false)]
    public async Task CoachCannotReplaceOrClearProtectedPlanAmongMultipleLegacyActivePlans(PlanPurpose purpose, bool assign)
    {
        var coach = await SeedUserAsync("multi-coach", "multi-coach@example.test");
        var owner = await SeedUserAsync("multi-owner", "multi-owner@example.test");
        var personal = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "First active", Purpose = PlanPurpose.Personal, IsActive = true };
        var protectedPlan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Protected active", Purpose = purpose, IsActive = true };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id });
            db.Plans.AddRange(personal, protectedPlan);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(coach.Id);

        var action = assign ? $"{personal.Id}/assign" : "unassign";
        var response = await Client.PostAsync($"/api/trainer/trainees/{owner.Id}/plans/{action}", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var verification = Factory.Services.CreateScope();
        var activeIds = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans
            .Where(plan => plan.UserId == owner.Id && plan.IsActive).Select(plan => plan.Id).ToListAsync();
        Assert.That(activeIds, Is.EquivalentTo(new[] { personal.Id, protectedPlan.Id }));
    }
}
