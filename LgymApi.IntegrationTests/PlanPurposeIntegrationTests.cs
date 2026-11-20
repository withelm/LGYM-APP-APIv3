using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task AssignedTemplateCreatesIndependentPersonalPlan()
    {
        var coach = await SeedUserAsync("assignment-coach", "assignment-coach@example.test");
        var trainee = await SeedUserAsync("assignment-trainee", "assignment-trainee@example.test");
        var source = new Plan { Id = Id<Plan>.New(), UserId = coach.Id, Name = "Template", Purpose = PlanPurpose.Professional, IsActive = false };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = source.Id, Name = "Template day" };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, coach.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = trainee.Id });
            db.AddRange(source, day);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(coach.Id);
        using var assigned = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/plans/{source.Id}/assign", null);
        Assert.That(assigned.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        string copyId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var copy = await db.Plans.AsNoTracking().SingleAsync(p => p.UserId == trainee.Id);
            Assert.That(copy.Id, Is.Not.EqualTo(source.Id));
            Assert.That(copy.Purpose, Is.EqualTo(PlanPurpose.Personal));
            copyId = copy.Id.ToString();
            var copiedDay = await db.PlanDays.AsNoTracking().SingleAsync(d => d.PlanId == copy.Id);
            Assert.That(copiedDay.Id, Is.Not.EqualTo(day.Id));
            Assert.That(copiedDay.Name, Is.EqualTo(day.Name));
        }
        SetAuthorizationHeader(trainee.Id);
        using var editCopy = await PostAsJsonWithApiOptionsAsync($"/api/{trainee.Id}/updatePlan", new { _id = copyId, name = "Personal edit", purpose = "Personal" });
        Assert.That(editCopy.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        SetAuthorizationHeader(coach.Id);
        using var editSource = await PostAsJsonWithApiOptionsAsync($"/api/{coach.Id}/updatePlan", new { _id = source.Id.ToString(), name = "Template edit", purpose = "Professional" });
        Assert.That(editSource.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var verification = Factory.Services.CreateScope();
        var plans = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Plans.AsNoTracking().ToListAsync();
        Assert.That(plans.Single(p => p.Id == source.Id).Name, Is.EqualTo("Template edit"));
        Assert.That(plans.Single(p => p.Id.ToString() == copyId).Name, Is.EqualTo("Personal edit"));
        Assert.That(plans.Single(p => p.Id.ToString() == copyId).Purpose, Is.EqualTo(PlanPurpose.Personal));
    }

    [Test]
    public async Task SharedCopiesRemainIndependentAndCodeWorksAfterUnlink()
    {
        var owner = await SeedUserAsync("copy-coach", "copy-coach@example.test");
        var recipient = await SeedUserAsync("copy-trainee", "copy-trainee@example.test");
        var source = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Source", Purpose = PlanPurpose.Professional };
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, owner.Id));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = owner.Id, TraineeId = recipient.Id });
            db.Plans.Add(source);
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(owner.Id);
        using var share = await Client.PostAsync($"/api/{source.Id}/share", null);
        Assert.That(share.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var shareBody = JsonDocument.Parse(await share.Content.ReadAsStringAsync());
        var code = shareBody.RootElement.GetProperty("shareCode").GetString();
        SetAuthorizationHeader(recipient.Id);
        using var first = await Client.PostAsJsonAsync("/api/copy", new { shareCode = code });
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var firstId = firstBody.RootElement.GetProperty("id").GetString();
        Assert.That(firstBody.RootElement.GetProperty("purpose").GetString(), Is.EqualTo("Unclassified"));

        SetAuthorizationHeader(owner.Id);
        using var unlink = await Client.PostAsync($"/api/trainer/trainees/{recipient.Id}/unlink", null);
        Assert.That(unlink.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        SetAuthorizationHeader(recipient.Id);
        using var second = await Client.PostAsJsonAsync("/api/copy", new { shareCode = code });
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var secondBody = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        var secondId = secondBody.RootElement.GetProperty("id").GetString();
        Assert.That(secondBody.RootElement.GetProperty("purpose").GetString(), Is.EqualTo("Unclassified"));
        Assert.That(firstId, Is.Not.EqualTo(secondId));
        Assert.That(secondId, Is.Not.EqualTo(source.Id.ToString()));

        using var denied = await PostAsJsonWithApiOptionsAsync($"/api/{recipient.Id}/updatePlan", new { _id = source.Id.ToString(), name = "Intrusion" });
        Assert.That(denied.IsSuccessStatusCode, Is.False);
        using var editCopy = await PostAsJsonWithApiOptionsAsync($"/api/{recipient.Id}/updatePlan", new { _id = firstId, name = "Copy edited" });
        Assert.That(editCopy.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        SetAuthorizationHeader(owner.Id);
        using var editSource = await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = source.Id.ToString(), name = "Source edited", purpose = "Professional" });
        Assert.That(editSource.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var plans = await dbVerify.Plans.AsNoTracking().Where(p => p.UserId == owner.Id || p.UserId == recipient.Id).ToListAsync();
        Assert.That(plans, Has.Exactly(3).Items);
        Assert.That(plans.Single(p => p.Id == source.Id).Name, Is.EqualTo("Source edited"));
        Assert.That(plans.Single(p => p.Id.ToString() == firstId).Name, Is.EqualTo("Copy edited"));
        Assert.That(plans.Single(p => p.Id.ToString() == secondId).Name, Is.EqualTo("Source"));
        Assert.That((plans.Where(p => p.UserId == recipient.Id)).All(p => p.Purpose == PlanPurpose.Unclassified), Is.True);
    }

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

        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var rows = json.RootElement.EnumerateArray().ToArray();
        Assert.That(rows, Has.Exactly(1).Items);
        Assert.That(rows[0].GetProperty("_id").GetString(), Is.EqualTo(personal.Id.ToString()));
        Assert.That(personalDays.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(libraryDays.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(downstreamDays.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(indirectList.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
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
        Assert.That(personalUpdate.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(professionalUpdate.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var response = await Client.GetAsync($"/api/{owner.Id}/getPlansList?purpose=Personal");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = json.RootElement.EnumerateArray().ToArray();
        Assert.That(rows, Has.Exactly(1).Items);
        Assert.That(rows[0].GetProperty("_id").GetString(), Is.EqualTo(personal.Id.ToString()));
        Assert.That(rows[0].GetProperty("purpose").GetString(), Is.EqualTo("Personal"));
        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var preserved = await dbVerify.Plans.AsNoTracking().SingleAsync(plan => plan.Id == professional.Id);
        Assert.That(preserved.IsActive, Is.True);
        Assert.That(preserved.Purpose, Is.EqualTo(PlanPurpose.Professional));
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
        Assert.That(shareResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var share = JsonDocument.Parse(await shareResponse.Content.ReadAsStringAsync());
        var code = share.RootElement.GetProperty("shareCode").GetString();
        SetAuthorizationHeader(recipient.Id);

        var response = await Client.PostAsJsonAsync("/api/copy", new { shareCode = code });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var copy = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var copyId = copy.RootElement.GetProperty("id").GetString();
        Assert.That(copyId, Is.Not.EqualTo(source.Id.ToString()));
        Assert.That(copy.RootElement.GetProperty("purpose").GetString(), Is.EqualTo("Unclassified"));
        using var verification = Factory.Services.CreateScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var plans = await dbVerify.Plans.AsNoTracking().ToListAsync();
        Assert.That((plans).Where(plan => plan.Id == source.Id && plan.UserId == owner.Id && plan.Purpose == PlanPurpose.Professional), Has.Exactly(1).Items);
        Assert.That((plans).Where(plan => plan.Id.ToString() == copyId && plan.UserId == recipient.Id && plan.Purpose == PlanPurpose.Unclassified), Has.Exactly(1).Items);
        SetAuthorizationHeader(coach.Id);
        var coachList = await Client.GetAsync($"/api/trainer/trainees/{recipient.Id}/plans");
        Assert.That(coachList.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var coachJson = JsonDocument.Parse(await coachList.Content.ReadAsStringAsync());
        Assert.That(coachJson.RootElement.GetArrayLength(), Is.EqualTo(0));
    }
}
