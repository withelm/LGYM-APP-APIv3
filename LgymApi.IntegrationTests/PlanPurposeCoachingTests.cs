using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.EnumerateArray().Should().ContainSingle();
        json.RootElement[0].GetProperty("name").GetString().Should().Be("Personal");
        var indirect = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/plans");
        indirect.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var verification = Factory.Services.CreateScope();
        var persisted = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.IgnoreQueryFilters().SingleAsync(item => item.Id == plan.Id);
        persisted.Name.Should().Be("Private");
        persisted.Purpose.Should().Be(purpose);
        persisted.IsDeleted.Should().BeFalse();
        persisted.IsActive.Should().BeFalse();
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

        created.IsSuccessStatusCode.Should().BeTrue();
        using var verification = Factory.Services.CreateScope();
        var plans = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.AsNoTracking().ToListAsync();
        plans.Single(item => item.Name == "Library").IsActive.Should().BeFalse();
        plans.Single(item => item.Id == active.Id).IsActive.Should().BeTrue();
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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var verification = Factory.Services.CreateScope();
        (await verification.ServiceProvider.GetRequiredService<AppDbContext>().Trainings.AnyAsync(t => t.UserId == actor.Id)).Should().BeFalse();
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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var verification = Factory.Services.CreateScope();
        var activeIds = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans
            .Where(plan => plan.UserId == owner.Id && plan.IsActive).Select(plan => plan.Id).ToListAsync();
        activeIds.Should().BeEquivalentTo(new[] { personal.Id, protectedPlan.Id });
    }
}
