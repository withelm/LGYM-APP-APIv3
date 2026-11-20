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
public sealed class PlanAuthorizationBoundaryTests : IntegrationTestBase
{
    [TestCase(PlanPurpose.Personal)]
    [TestCase(PlanPurpose.Professional)]
    [TestCase(PlanPurpose.Unclassified)]
    public async Task OwnerCanReadAndUpdateOwnPlanDayForEachPurpose(PlanPurpose purpose)
    {
        var owner = await SeedUserAsync("purpose-owner", "purpose-owner@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Own plan", Purpose = purpose, IsActive = true };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Own day" };
        var exercise = new Exercise { Id = Id<Exercise>.New(), UserId = owner.Id, Name = "Own exercise", BodyPart = BodyParts.Chest };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(plan, day, exercise, new PlanDayExercise { Id = Id<PlanDayExercise>.New(), PlanDayId = day.Id, ExerciseId = exercise.Id, Series = 3, Reps = "10" });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(owner.Id);

        using var list = await Client.GetAsync($"/api/{owner.Id}/getPlansList");
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.That((listJson.RootElement.EnumerateArray()).Where(row => row.GetProperty("_id").GetString() == plan.Id.ToString()), Has.Exactly(1).Items);
        using var read = await Client.GetAsync($"/api/planDay/{day.Id}/getPlanDay");
        Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var dayJson = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.That(dayJson.RootElement.GetProperty("exercises")[0].GetProperty("exercise").GetProperty("_id").GetString(), Is.EqualTo(exercise.Id.ToString()));
        using var update = await PostAsJsonWithApiOptionsAsync("/api/planDay/updatePlanDay", new
        {
            _id = day.Id.ToString(), name = "Updated day",
            exercises = new[] { new { exercise = exercise.Id.ToString(), series = 5, reps = "8" } }
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var verification = Factory.Services.CreateScope();
        var persisted = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await persisted.PlanDays.AsNoTracking().SingleAsync(d => d.Id == day.Id)).Name, Is.EqualTo("Updated day"));
        var storedExercise = await persisted.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == day.Id);
        Assert.That(storedExercise.ExerciseId, Is.EqualTo(exercise.Id));
        Assert.That(storedExercise.Series, Is.EqualTo(5));
        Assert.That(storedExercise.Reps, Is.EqualTo("8"));
        Assert.That((await persisted.Plans.AsNoTracking().SingleAsync(p => p.Id == plan.Id)).Purpose, Is.EqualTo(purpose));
    }

    [TestCase("assign")]
    [TestCase("unassign")]
    public async Task AnonymousCannotChangeManagedPlanActivation(string action)
    {
        var owner = await SeedUserAsync("anonymous-owner", "anonymous-owner@example.test");
        var active = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Active", Purpose = PlanPurpose.Personal, IsActive = true };
        var target = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Target", Purpose = PlanPurpose.Personal, IsActive = false };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.AddRange(active, target);
            await db.SaveChangesAsync();
        }
        ClearAuthorizationHeader();

        var path = action == "assign" ? $"{target.Id}/assign" : "unassign";
        using var response = await Client.PostAsync($"/api/trainer/trainees/{owner.Id}/plans/{path}", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var verification = Factory.Services.CreateScope();
        var plans = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.UserId == owner.Id).ToListAsync();
        Assert.That(plans, Has.Exactly(2).Items);
        Assert.That(plans.Single(p => p.Id == active.Id).IsActive, Is.True);
        Assert.That(plans.Single(p => p.Id == target.Id).IsActive, Is.False);
        Assert.That((plans).All(p => !p.IsDeleted && p.Purpose == PlanPurpose.Personal), Is.True);
    }

    [Test]
    public async Task CurrentCoachCanDeleteTraineePersonalPlan()
    {
        var owner = await SeedUserAsync("delete-owner", "delete-owner@example.test");
        var coach = await SeedUserAsync("delete-coach", "delete-coach@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Trainee plan", Purpose = PlanPurpose.Personal, IsActive = false };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id });
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(coach.Id);

        using var response = await Client.PostAsync($"/api/trainer/trainees/{owner.Id}/plans/{plan.Id}/delete", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var verification = Factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        Assert.That(stored.UserId, Is.EqualTo(owner.Id));
        Assert.That(stored.Purpose, Is.EqualTo(PlanPurpose.Personal));
        Assert.That(stored.IsDeleted, Is.True);
    }
}
