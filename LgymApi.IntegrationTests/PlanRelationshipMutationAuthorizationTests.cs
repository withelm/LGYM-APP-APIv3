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
public sealed class PlanRelationshipMutationAuthorizationTests : IntegrationTestBase
{
    [TestCase("update")]
    [TestCase("delete")]
    [TestCase("assign")]
    [TestCase("unassign")]
    public async Task FormerCoachCannotMutatePersonalPlanAfterUnlink(string operation)
    {
        var owner = await SeedUserAsync("relationship-owner", "relationship-owner@example.test");
        var coach = await SeedUserAsync("relationship-coach", "relationship-coach@example.test");
        var active = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Active", Purpose = PlanPurpose.Personal, IsActive = true };
        var target = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Target", Purpose = PlanPurpose.Personal, IsActive = false };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            var link = new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id };
            db.TrainerTraineeLinks.Add(link);
            db.Plans.AddRange(active, target);
            await db.SaveChangesAsync();
            db.TrainerTraineeLinks.Remove(link);
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(coach.Id);

        var path = operation == "unassign" ? "unassign" : $"{target.Id}/{operation}";
        using var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{owner.Id}/plans/{path}", new { name = "Intrusion" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var plans = await dbVerify.Plans.IgnoreQueryFilters().AsNoTracking().Where(p => p.UserId == owner.Id).ToListAsync();
        Assert.That(plans, Has.Exactly(2).Items);
        var persistedTarget = plans.Single(p => p.Id == target.Id);
        Assert.That(persistedTarget.Name, Is.EqualTo("Target"));
        Assert.That(persistedTarget.Purpose, Is.EqualTo(PlanPurpose.Personal));
        Assert.That(persistedTarget.IsDeleted, Is.False);
        Assert.That(persistedTarget.IsActive, Is.False);
        var persistedActive = plans.Single(p => p.Id == active.Id);
        Assert.That(persistedActive.Name, Is.EqualTo("Active"));
        Assert.That(persistedActive.IsActive, Is.True);
        Assert.That(persistedActive.IsDeleted, Is.False);
    }
}
