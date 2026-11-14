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
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        listJson.RootElement.EnumerateArray().Should().ContainSingle(row => row.GetProperty("_id").GetString() == plan.Id.ToString());
        using var read = await Client.GetAsync($"/api/planDay/{day.Id}/getPlanDay");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        using var dayJson = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        dayJson.RootElement.GetProperty("exercises")[0].GetProperty("exercise").GetProperty("_id").GetString().Should().Be(exercise.Id.ToString());
        using var update = await PostAsJsonWithApiOptionsAsync("/api/planDay/updatePlanDay", new
        {
            _id = day.Id.ToString(), name = "Updated day",
            exercises = new[] { new { exercise = exercise.Id.ToString(), series = 5, reps = "8" } }
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verification = Factory.Services.CreateScope();
        var persisted = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        (await persisted.PlanDays.AsNoTracking().SingleAsync(d => d.Id == day.Id)).Name.Should().Be("Updated day");
        var storedExercise = await persisted.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == day.Id);
        storedExercise.ExerciseId.Should().Be(exercise.Id);
        storedExercise.Series.Should().Be(5);
        storedExercise.Reps.Should().Be("8");
        (await persisted.Plans.AsNoTracking().SingleAsync(p => p.Id == plan.Id)).Purpose.Should().Be(purpose);
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

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var verification = Factory.Services.CreateScope();
        var plans = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.UserId == owner.Id).ToListAsync();
        plans.Should().HaveCount(2);
        plans.Single(p => p.Id == active.Id).IsActive.Should().BeTrue();
        plans.Single(p => p.Id == target.Id).IsActive.Should().BeFalse();
        plans.Should().OnlyContain(p => !p.IsDeleted && p.Purpose == PlanPurpose.Personal);
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verification = Factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        stored.UserId.Should().Be(owner.Id);
        stored.Purpose.Should().Be(PlanPurpose.Personal);
        stored.IsDeleted.Should().BeTrue();
    }
}
