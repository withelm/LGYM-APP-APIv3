using System.Net;
using System.Net.Http.Json;
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
public sealed class PlanAuthorizationRemainingCasesTests : IntegrationTestBase
{
    [TestCase(PlanPurpose.Professional, "former")]
    [TestCase(PlanPurpose.Unclassified, "former")]
    [TestCase(PlanPurpose.Professional, "foreign")]
    [TestCase(PlanPurpose.Unclassified, "foreign")]
    [TestCase(PlanPurpose.Professional, "admin")]
    [TestCase(PlanPurpose.Unclassified, "admin")]
    [TestCase(PlanPurpose.Professional, "indirect")]
    [TestCase(PlanPurpose.Unclassified, "indirect")]
    public async Task NonOwnerCannotReadOrChangeNonPersonalDay(PlanPurpose purpose, string actor)
    {
        var owner = await SeedUserAsync("private-owner", "private-owner@example.test");
        var former = await SeedUserAsync("private-former", "private-former@example.test");
        var foreign = await SeedUserAsync("private-foreign", "private-foreign@example.test");
        var indirect = await SeedUserAsync("private-indirect", "private-indirect@example.test");
        var middle = await SeedUserAsync("private-middle", "private-middle@example.test");
        var admin = await SeedAdminAsync();
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Private plan", Purpose = purpose, IsActive = false };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Private day" };
        var exercise = new Exercise { Id = Id<Exercise>.New(), UserId = owner.Id, Name = "Private exercise", BodyPart = BodyParts.Chest };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, former.Id));
            db.UserRoles.AddRange(TestDataFactory.ApprovedTrainerRole(db, indirect.Id), TestDataFactory.ApprovedTrainerRole(db, middle.Id));
            db.TrainerTraineeLinks.AddRange(
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = indirect.Id, TraineeId = middle.Id },
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = middle.Id, TraineeId = owner.Id });
            var link = new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = former.Id, TraineeId = owner.Id };
            db.TrainerTraineeLinks.Add(link);
            db.AddRange(plan, day, exercise, new PlanDayExercise { Id = Id<PlanDayExercise>.New(), PlanDayId = day.Id, ExerciseId = exercise.Id, Series = 3, Reps = "10" });
            await db.SaveChangesAsync();
            db.TrainerTraineeLinks.Remove(link);
            await db.SaveChangesAsync();
        }
        var account = actor switch { "former" => former, "foreign" => foreign, "admin" => admin, "indirect" => indirect, _ => throw new ArgumentOutOfRangeException(nameof(actor)) };
        SetAuthorizationHeader(account.Id);

        using var read = await Client.GetAsync($"/api/planDay/{day.Id}/getPlanDay");
        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await read.Content.ReadAsStringAsync()).Should().NotContain("Private exercise");
        using var update = await PostAsJsonWithApiOptionsAsync("/api/planDay/updatePlanDay", new
        {
            _id = day.Id.ToString(), name = "Intrusion",
            exercises = new[] { new { exercise = exercise.Id.ToString(), series = 5, reps = "8" } }
        });
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var delete = await Client.GetAsync($"/api/planDay/{day.Id}/deletePlanDay");
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedDay = await dbVerify.PlanDays.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == day.Id);
        storedDay.Name.Should().Be("Private day");
        storedDay.IsDeleted.Should().BeFalse();
        var storedExercise = await dbVerify.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == day.Id);
        storedExercise.ExerciseId.Should().Be(exercise.Id);
        storedExercise.Series.Should().Be(3);
        storedExercise.Reps.Should().Be("10");
        var storedPlan = await dbVerify.Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        storedPlan.Purpose.Should().Be(purpose);
        storedPlan.IsDeleted.Should().BeFalse();
    }

    [TestCase(PlanPurpose.Personal)]
    [TestCase(PlanPurpose.Professional)]
    [TestCase(PlanPurpose.Unclassified)]
    public async Task OwnerCanDeleteOwnPlanForEachPurpose(PlanPurpose purpose)
    {
        var owner = await SeedUserAsync("delete-purpose-owner", "delete-purpose-owner@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Own plan", Purpose = purpose, IsActive = false };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Own day" };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AddRange(plan, day);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(owner.Id);

        using var response = await Client.PostAsync($"/api/{plan.Id}/deletePlan", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedPlan = await dbVerify.Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        storedPlan.UserId.Should().Be(owner.Id);
        storedPlan.Purpose.Should().Be(purpose);
        storedPlan.IsDeleted.Should().BeTrue();
        (await dbVerify.PlanDays.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == day.Id)).IsDeleted.Should().BeTrue();
    }

    [TestCase("update")]
    [TestCase("delete")]
    public async Task AnonymousCannotUpdateOrDeleteManagedPlan(string action)
    {
        var owner = await SeedUserAsync("anonymous-managed-owner", "anonymous-managed-owner@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Original", Purpose = PlanPurpose.Personal, IsActive = true };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }
        ClearAuthorizationHeader();

        using var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{owner.Id}/plans/{plan.Id}/{action}", new { name = "Intrusion" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var verification = Factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        stored.UserId.Should().Be(owner.Id);
        stored.Name.Should().Be("Original");
        stored.Purpose.Should().Be(PlanPurpose.Personal);
        stored.IsActive.Should().BeTrue();
        stored.IsDeleted.Should().BeFalse();
    }
}
