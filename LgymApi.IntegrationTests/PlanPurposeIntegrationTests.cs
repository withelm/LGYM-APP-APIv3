using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PlanPurposeIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task ChainedCoachingExposesOnlyDirectTraineePersonalPlans()
    {
        var coach = await SeedUserAsync("chain-coach", "chain-coach@example.test");
        var middle = await SeedUserAsync("chain-middle", "chain-middle@example.test");
        var trainee = await SeedUserAsync("chain-trainee", "chain-trainee@example.test");
        var personal = new Plan { Id = Id<Plan>.New(), UserId = middle.Id, Name = "Personal", Purpose = PlanPurpose.Personal, IsActive = false };
        var library = new Plan { Id = Id<Plan>.New(), UserId = middle.Id, Name = "Library", Purpose = PlanPurpose.Professional, IsActive = false };
        var downstream = new Plan { Id = Id<Plan>.New(), UserId = trainee.Id, Name = "Downstream", Purpose = PlanPurpose.Personal, IsActive = false };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, middle.Id));
            db.TrainerTraineeLinks.AddRange(
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = middle.Id },
                new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = middle.Id, TraineeId = trainee.Id });
            db.Plans.AddRange(personal, library, downstream);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(coach.Id);
        var list = await Client.GetAsync($"/api/trainer/trainees/{middle.Id}/plans");
        var personalDays = await Client.GetAsync($"/api/planDay/{personal.Id}/getPlanDaysInfo");
        var libraryDays = await Client.GetAsync($"/api/planDay/{library.Id}/getPlanDaysInfo");
        var downstreamDays = await Client.GetAsync($"/api/planDay/{downstream.Id}/getPlanDaysInfo");
        var indirectList = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/plans");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var rows = json.RootElement.EnumerateArray().ToArray();
        rows.Should().ContainSingle();
        rows[0].GetProperty("_id").GetString().Should().Be(personal.Id.ToString());
        personalDays.StatusCode.Should().Be(HttpStatusCode.OK);
        libraryDays.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        downstreamDays.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        indirectList.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task OwnerClassifiesExistingPlanAndFiltersWithoutReplacingItsId()
    {
        var owner = await SeedUserAsync();
        var personal = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Personal", IsActive = false };
        var professional = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Library", IsActive = true };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.AddRange(personal, professional);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(owner.Id);
        var personalUpdate = await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = personal.Id.ToString(), name = personal.Name, purpose = "Personal" });
        var professionalUpdate = await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = professional.Id.ToString(), name = professional.Name, purpose = "Professional" });
        personalUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        professionalUpdate.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Client.GetAsync($"/api/{owner.Id}/getPlansList?purpose=Personal");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = json.RootElement.EnumerateArray().ToArray();
        rows.Should().ContainSingle();
        rows[0].GetProperty("_id").GetString().Should().Be(personal.Id.ToString());
        rows[0].GetProperty("purpose").GetString().Should().Be("Personal");
        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var preserved = await dbVerify.Plans.AsNoTracking().SingleAsync(plan => plan.Id == professional.Id);
        preserved.IsActive.Should().BeTrue();
        preserved.Purpose.Should().Be(PlanPurpose.Professional);
    }

    [Test]
    public async Task ShareCodeCopiesProfessionalPlanForUnrelatedAccountWithoutChangingSource()
    {
        var owner = await SeedUserAsync("purpose-source", "purpose-source@example.test");
        var recipient = await SeedUserAsync("purpose-recipient", "purpose-recipient@example.test");
        var coach = await SeedUserAsync("recipient-coach", "recipient-coach@example.test");
        var source = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Library", Purpose = PlanPurpose.Professional };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = recipient.Id });
            db.Plans.Add(source);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(owner.Id);
        var shareResponse = await Client.PostAsync($"/api/{source.Id}/share", null);
        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var share = JsonDocument.Parse(await shareResponse.Content.ReadAsStringAsync());
        var code = share.RootElement.GetProperty("shareCode").GetString();
        SetAuthorizationHeader(recipient.Id);

        var response = await Client.PostAsJsonAsync("/api/copy", new { shareCode = code });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var copy = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var copyId = copy.RootElement.GetProperty("id").GetString();
        copyId.Should().NotBe(source.Id.ToString());
        copy.RootElement.GetProperty("purpose").GetString().Should().Be("Unclassified");
        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var plans = await dbVerify.Plans.AsNoTracking().ToListAsync();
        plans.Should().ContainSingle(plan => plan.Id == source.Id && plan.UserId == owner.Id && plan.Purpose == PlanPurpose.Professional);
        plans.Should().ContainSingle(plan => plan.Id.ToString() == copyId && plan.UserId == recipient.Id && plan.Purpose == PlanPurpose.Unclassified);
        SetAuthorizationHeader(coach.Id);
        var coachList = await Client.GetAsync($"/api/trainer/trainees/{recipient.Id}/plans");
        coachList.StatusCode.Should().Be(HttpStatusCode.OK);
        using var coachJson = JsonDocument.Parse(await coachList.Content.ReadAsStringAsync());
        coachJson.RootElement.GetArrayLength().Should().Be(0);
    }
}
