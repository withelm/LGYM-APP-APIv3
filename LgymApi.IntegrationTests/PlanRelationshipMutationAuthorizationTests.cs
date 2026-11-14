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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var plans = await dbVerify.Plans.IgnoreQueryFilters().AsNoTracking().Where(p => p.UserId == owner.Id).ToListAsync();
        plans.Should().HaveCount(2);
        var persistedTarget = plans.Single(p => p.Id == target.Id);
        persistedTarget.Name.Should().Be("Target");
        persistedTarget.Purpose.Should().Be(PlanPurpose.Personal);
        persistedTarget.IsDeleted.Should().BeFalse();
        persistedTarget.IsActive.Should().BeFalse();
        var persistedActive = plans.Single(p => p.Id == active.Id);
        persistedActive.Name.Should().Be("Active");
        persistedActive.IsActive.Should().BeTrue();
        persistedActive.IsDeleted.Should().BeFalse();
    }
}
