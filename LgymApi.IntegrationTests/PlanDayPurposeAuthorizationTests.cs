using System.Net;
using System.Text.Json;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PlanDayPurposeAuthorizationTests : IntegrationTestBase
{
    [TestCase(PlanPurpose.Personal, true)]
    [TestCase(PlanPurpose.Professional, false)]
    [TestCase(PlanPurpose.Unclassified, false)]
    public async Task LinkedCoachCanReadAndChangeDayOnlyForPersonalPlans(PlanPurpose purpose, bool allowed)
    {
        var owner = await SeedUserAsync("day-owner", "day-owner@example.test");
        var coach = await SeedUserAsync("day-coach", "day-coach@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Plan", Purpose = purpose };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Original day" };
        var exercise = new Exercise { Id = Id<Exercise>.New(), UserId = owner.Id, Name = "Original exercise", BodyPart = BodyParts.Chest };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id });
            db.AddRange(plan, day, exercise, new PlanDayExercise { Id = Id<PlanDayExercise>.New(), PlanDayId = day.Id, ExerciseId = exercise.Id, Series = 3, Reps = "10" });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(coach.Id);

        using var read = await Client.GetAsync($"/api/planDay/{day.Id}/getPlanDay");
        Assert.That(read.StatusCode, Is.EqualTo(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden));
        if (allowed)
        {
            using var json = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
            Assert.That(json.RootElement.GetProperty("exercises")[0].GetProperty("exercise").GetProperty("_id").GetString(), Is.EqualTo(exercise.Id.ToString()));
        }
        else
        {
            Assert.That((await read.Content.ReadAsStringAsync()), Does.Not.Contain("Original exercise"));
        }

        using var update = await PostAsJsonWithApiOptionsAsync("/api/planDay/updatePlanDay", new
        {
            _id = day.Id.ToString(), name = "Changed day",
            exercises = new[] { new { exercise = exercise.Id.ToString(), series = 5, reps = "8" } }
        });
        Assert.That(update.StatusCode, Is.EqualTo(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden));
        using var verification = Factory.Services.CreateScope();
        var persisted = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await persisted.PlanDays.AsNoTracking().SingleAsync(item => item.Id == day.Id)).Name, Is.EqualTo(allowed ? "Changed day" : "Original day"));
        var storedExercise = await persisted.PlanDayExercises.AsNoTracking().SingleAsync(item => item.PlanDayId == day.Id);
        Assert.That(storedExercise.ExerciseId, Is.EqualTo(exercise.Id));
        Assert.That(storedExercise.Series, Is.EqualTo(allowed ? 5 : 3));
        Assert.That(storedExercise.Reps, Is.EqualTo(allowed ? "8" : "10"));
        if (!allowed)
        {
            using var delete = await Client.GetAsync($"/api/planDay/{day.Id}/deletePlanDay");
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            using var afterDelete = Factory.Services.CreateScope();
            var db = afterDelete.ServiceProvider.GetRequiredService<AppDbContext>();
            var unchanged = await db.PlanDays.IgnoreQueryFilters().AsNoTracking().SingleAsync(item => item.Id == day.Id);
            Assert.That(unchanged.IsDeleted, Is.False);
            Assert.That(unchanged.Name, Is.EqualTo("Original day"));
            Assert.That((await db.PlanDayExercises.AsNoTracking().SingleAsync(item => item.PlanDayId == day.Id)).Series, Is.EqualTo(3));
        }
    }

    [TestCase("owner", HttpStatusCode.OK)]
    [TestCase("coach", HttpStatusCode.OK)]
    [TestCase("former", HttpStatusCode.Forbidden)]
    [TestCase("foreign", HttpStatusCode.Forbidden)]
    [TestCase("admin", HttpStatusCode.Forbidden)]
    [TestCase("indirect", HttpStatusCode.Forbidden)]
    [TestCase("anonymous", HttpStatusCode.Unauthorized)]
    public async Task PersonalDayDirectIdRequiresOwnershipOrCurrentDirectRelationship(string actor, HttpStatusCode expected)
    {
        var owner = await SeedUserAsync("actor-owner", "actor-owner@example.test");
        var former = await SeedUserAsync("actor-former", "actor-former@example.test");
        var coach = await SeedUserAsync("actor-coach", "actor-coach@example.test");
        var foreign = await SeedUserAsync("actor-foreign", "actor-foreign@example.test");
        var indirect = await SeedUserAsync("actor-indirect", "actor-indirect@example.test");
        var middle = await SeedUserAsync("actor-middle", "actor-middle@example.test");
        var admin = await SeedAdminAsync();
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Private plan", Purpose = PlanPurpose.Personal };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Private day" };
        var exercise = new Exercise { Id = Id<Exercise>.New(), UserId = owner.Id, Name = "Private exercise", BodyPart = BodyParts.Chest };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.AddRange(TestDataFactory.ApprovedTrainerRole(db, former.Id), TestDataFactory.ApprovedTrainerRole(db, coach.Id), TestDataFactory.ApprovedTrainerRole(db, indirect.Id), TestDataFactory.ApprovedTrainerRole(db, middle.Id));
            db.TrainerTraineeLinks.AddRange(
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id },
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = indirect.Id, TraineeId = middle.Id },
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = middle.Id, TraineeId = owner.Id });
            var oldLink = new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = former.Id, TraineeId = owner.Id };
            db.TrainerTraineeLinks.Add(oldLink);
            db.AddRange(plan, day, exercise, new PlanDayExercise { Id = Id<PlanDayExercise>.New(), PlanDayId = day.Id, ExerciseId = exercise.Id, Series = 3, Reps = "10" });
            await db.SaveChangesAsync();
            db.TrainerTraineeLinks.Remove(oldLink);
            await db.SaveChangesAsync();
        }

        var account = actor switch
        {
            "owner" => owner,
            "coach" => coach,
            "former" => former,
            "foreign" => foreign,
            "admin" => admin,
            "indirect" => indirect,
            _ => null
        };
        if (account is not null)
        {
            SetAuthorizationHeader(account.Id);
        }

        using var read = await Client.GetAsync($"/api/planDay/{day.Id}/getPlanDay");
        Assert.That(read.StatusCode, Is.EqualTo(expected));
        if (expected != HttpStatusCode.OK)
        {
            Assert.That((await read.Content.ReadAsStringAsync()), Does.Not.Contain("Private exercise"));
            using var update = await PostAsJsonWithApiOptionsAsync("/api/planDay/updatePlanDay", new
            {
                _id = day.Id.ToString(), name = "Intrusion",
                exercises = new[] { new { exercise = exercise.Id.ToString(), series = 5, reps = "8" } }
            });
            Assert.That(update.StatusCode, Is.EqualTo(expected));
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.That((await db.PlanDays.AsNoTracking().SingleAsync(item => item.Id == day.Id)).Name, Is.EqualTo("Private day"));
            var stored = await db.PlanDayExercises.AsNoTracking().SingleAsync(item => item.PlanDayId == day.Id);
            Assert.That(stored.Series, Is.EqualTo(3));
            Assert.That(stored.Reps, Is.EqualTo("10"));
        }

        using var delete = await Client.GetAsync($"/api/planDay/{day.Id}/deletePlanDay");
        Assert.That(delete.StatusCode, Is.EqualTo(expected));
        using var verification = Factory.Services.CreateScope();
        var verificationDb = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await verificationDb.PlanDays.IgnoreQueryFilters().AsNoTracking().SingleAsync(item => item.Id == day.Id);
        Assert.That(persisted.IsDeleted, Is.EqualTo(expected == HttpStatusCode.OK));
        Assert.That(persisted.Name, Is.EqualTo("Private day"));
        if (expected != HttpStatusCode.OK)
        {
            var stored = await verificationDb.PlanDayExercises.AsNoTracking().SingleAsync(item => item.PlanDayId == day.Id);
            Assert.That(stored.ExerciseId, Is.EqualTo(exercise.Id));
            Assert.That(stored.Series, Is.EqualTo(3));
            Assert.That(stored.Reps, Is.EqualTo("10"));
        }
    }

    [Test]
    public async Task OwnerClassificationControlsExistingCoachAccessImmediately()
    {
        var owner = await SeedUserAsync("classification-owner", "classification-owner@example.test");
        var coach = await SeedUserAsync("classification-coach", "classification-coach@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Plan", Purpose = PlanPurpose.Unclassified };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Day" };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id });
            db.AddRange(plan, day);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(coach.Id);
        Assert.That((await Client.GetAsync($"/api/planDay/{day.Id}/getPlanDay")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        SetAuthorizationHeader(owner.Id);
        Assert.That((await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "Plan", purpose = "Personal" })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        SetAuthorizationHeader(coach.Id);
        Assert.That((await Client.GetAsync($"/api/planDay/{day.Id}/getPlanDay")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        SetAuthorizationHeader(owner.Id);
        Assert.That((await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "Plan", purpose = "Professional" })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        SetAuthorizationHeader(coach.Id);
        Assert.That((await Client.GetAsync($"/api/planDay/{day.Id}/getPlanDay")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
