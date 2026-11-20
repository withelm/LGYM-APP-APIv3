using System.Net;
using System.Net.Http.Json;
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
        Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await read.Content.ReadAsStringAsync()), Does.Not.Contain("Private exercise"));
        using var update = await PostAsJsonWithApiOptionsAsync("/api/planDay/updatePlanDay", new
        {
            _id = day.Id.ToString(), name = "Intrusion",
            exercises = new[] { new { exercise = exercise.Id.ToString(), series = 5, reps = "8" } }
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var delete = await Client.GetAsync($"/api/planDay/{day.Id}/deletePlanDay");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedDay = await dbVerify.PlanDays.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == day.Id);
        Assert.That(storedDay.Name, Is.EqualTo("Private day"));
        Assert.That(storedDay.IsDeleted, Is.False);
        var storedExercise = await dbVerify.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == day.Id);
        Assert.That(storedExercise.ExerciseId, Is.EqualTo(exercise.Id));
        Assert.That(storedExercise.Series, Is.EqualTo(3));
        Assert.That(storedExercise.Reps, Is.EqualTo("10"));
        var storedPlan = await dbVerify.Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        Assert.That(storedPlan.Purpose, Is.EqualTo(purpose));
        Assert.That(storedPlan.IsDeleted, Is.False);
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedPlan = await dbVerify.Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        Assert.That(storedPlan.UserId, Is.EqualTo(owner.Id));
        Assert.That(storedPlan.Purpose, Is.EqualTo(purpose));
        Assert.That(storedPlan.IsDeleted, Is.True);
        Assert.That((await dbVerify.PlanDays.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == day.Id)).IsDeleted, Is.True);
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var verification = Factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        Assert.That(stored.UserId, Is.EqualTo(owner.Id));
        Assert.That(stored.Name, Is.EqualTo("Original"));
        Assert.That(stored.Purpose, Is.EqualTo(PlanPurpose.Personal));
        Assert.That(stored.IsActive, Is.True);
        Assert.That(stored.IsDeleted, Is.False);
    }
}
