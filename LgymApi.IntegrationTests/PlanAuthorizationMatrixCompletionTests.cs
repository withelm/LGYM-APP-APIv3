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
public sealed class PlanAuthorizationMatrixCompletionTests : IntegrationTestBase
{
    [TestCase(PlanPurpose.Professional, "former", "list")]
    [TestCase(PlanPurpose.Unclassified, "former", "list")]
    [TestCase(PlanPurpose.Professional, "foreign", "list")]
    [TestCase(PlanPurpose.Unclassified, "foreign", "list")]
    [TestCase(PlanPurpose.Professional, "admin", "list")]
    [TestCase(PlanPurpose.Unclassified, "admin", "list")]
    [TestCase(PlanPurpose.Professional, "indirect", "list")]
    [TestCase(PlanPurpose.Unclassified, "indirect", "list")]
    [TestCase(PlanPurpose.Professional, "former", "update")]
    [TestCase(PlanPurpose.Unclassified, "former", "update")]
    [TestCase(PlanPurpose.Professional, "foreign", "update")]
    [TestCase(PlanPurpose.Unclassified, "foreign", "update")]
    [TestCase(PlanPurpose.Professional, "admin", "update")]
    [TestCase(PlanPurpose.Unclassified, "admin", "update")]
    [TestCase(PlanPurpose.Professional, "indirect", "update")]
    [TestCase(PlanPurpose.Unclassified, "indirect", "update")]
    [TestCase(PlanPurpose.Professional, "former", "delete")]
    [TestCase(PlanPurpose.Unclassified, "former", "delete")]
    [TestCase(PlanPurpose.Professional, "foreign", "delete")]
    [TestCase(PlanPurpose.Unclassified, "foreign", "delete")]
    [TestCase(PlanPurpose.Professional, "admin", "delete")]
    [TestCase(PlanPurpose.Unclassified, "admin", "delete")]
    [TestCase(PlanPurpose.Professional, "indirect", "delete")]
    [TestCase(PlanPurpose.Unclassified, "indirect", "delete")]
    [TestCase(PlanPurpose.Professional, "former", "assign")]
    [TestCase(PlanPurpose.Unclassified, "former", "assign")]
    [TestCase(PlanPurpose.Professional, "foreign", "assign")]
    [TestCase(PlanPurpose.Unclassified, "foreign", "assign")]
    [TestCase(PlanPurpose.Professional, "admin", "assign")]
    [TestCase(PlanPurpose.Unclassified, "admin", "assign")]
    [TestCase(PlanPurpose.Professional, "indirect", "assign")]
    [TestCase(PlanPurpose.Unclassified, "indirect", "assign")]
    [TestCase(PlanPurpose.Professional, "former", "unassign")]
    [TestCase(PlanPurpose.Unclassified, "former", "unassign")]
    [TestCase(PlanPurpose.Professional, "foreign", "unassign")]
    [TestCase(PlanPurpose.Unclassified, "foreign", "unassign")]
    [TestCase(PlanPurpose.Professional, "admin", "unassign")]
    [TestCase(PlanPurpose.Unclassified, "admin", "unassign")]
    [TestCase(PlanPurpose.Professional, "indirect", "unassign")]
    [TestCase(PlanPurpose.Unclassified, "indirect", "unassign")]
    public async Task NonCurrentTrainerCannotAccessOrMutateNonPersonalPlan(PlanPurpose purpose, string actor, string action)
    {
        var owner = await SeedUserAsync("matrix-owner", "matrix-owner@example.test");
        var trainer = actor == "admin" ? await SeedAdminAsync() : await SeedUserAsync("matrix-trainer", "matrix-trainer@example.test");
        var middle = await SeedUserAsync("matrix-middle", "matrix-middle@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Protected", Purpose = purpose, IsActive = action != "assign" };
        var predecessor = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Previous", Purpose = PlanPurpose.Personal, IsActive = true };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.AddRange(TestDataFactory.ApprovedTrainerRole(db, trainer.Id), TestDataFactory.ApprovedTrainerRole(db, middle.Id));
            var link = new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = owner.Id };
            if (actor == "former") db.TrainerTraineeLinks.Add(link);
            if (actor == "indirect")
            {
                db.TrainerTraineeLinks.AddRange(
                    new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = middle.Id },
                    new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = middle.Id, TraineeId = owner.Id });
            }
            db.Plans.Add(plan);
            if (action == "assign") db.Plans.Add(predecessor);
            await db.SaveChangesAsync();
            if (actor == "former")
            {
                db.TrainerTraineeLinks.Remove(link);
                await db.SaveChangesAsync();
            }
        }
        SetAuthorizationHeader(trainer.Id);
        var route = $"/api/trainer/trainees/{owner.Id}/plans";
        using var response = action switch
        {
            "list" => await Client.GetAsync(route),
            "unassign" => await Client.PostAsync($"{route}/unassign", null),
            _ => await Client.PostAsJsonAsync($"{route}/{plan.Id}/{action}", new { name = "Intrusion" })
        };
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Protected");
        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbVerify.Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        stored.UserId.Should().Be(owner.Id);
        stored.Name.Should().Be("Protected");
        stored.Purpose.Should().Be(purpose);
        stored.IsActive.Should().Be(action != "assign");
        stored.IsDeleted.Should().BeFalse();
        if (action == "assign")
        {
            var previous = await dbVerify.Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == predecessor.Id);
            previous.UserId.Should().Be(owner.Id);
            previous.Name.Should().Be("Previous");
            previous.Purpose.Should().Be(PlanPurpose.Personal);
            previous.IsActive.Should().BeTrue();
            previous.IsDeleted.Should().BeFalse();
        }
    }

    [TestCase(PlanPurpose.Personal)]
    [TestCase(PlanPurpose.Professional)]
    [TestCase(PlanPurpose.Unclassified)]
    public async Task OwnerCanFilterAndUpdatePlanOfEachPurpose(PlanPurpose purpose)
    {
        var owner = await SeedUserAsync("matrix-own", "matrix-own@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Before", Purpose = purpose, IsActive = false };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(owner.Id);
        using var list = await Client.GetAsync($"/api/{owner.Id}/getPlansList?purpose={purpose}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        json.RootElement.EnumerateArray().Should().ContainSingle(row => row.GetProperty("_id").GetString() == plan.Id.ToString());
        using var update = await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "After", purpose = purpose.ToString() });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verification = Factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.AsNoTracking().SingleAsync(p => p.Id == plan.Id);
        stored.Name.Should().Be("After");
        stored.Purpose.Should().Be(purpose);
        stored.UserId.Should().Be(owner.Id);
    }
}
