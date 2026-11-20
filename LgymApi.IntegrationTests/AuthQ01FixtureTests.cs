using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.IntegrationTests.Authorization;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed partial class AuthQ01FixtureTests : IntegrationTestBase
{
    [Test]
    public void CanonicalFixture_LoadsAndSatisfiesSchemaInvariants()
    {
        var fixture = AuthQ01FixtureLoader.Load();

        Assert.That(fixture.SchemaVersion, Is.EqualTo(1));
        Assert.That(fixture.FixtureId, Is.EqualTo("AUTH-Q01-v1"));
        Assert.That(fixture.SyntheticOnly, Is.True);
        Assert.That(fixture.Actors, Has.Exactly(9).Items);
        Assert.That(fixture.Reports, Has.Exactly(2).Items);
        Assert.That(fixture.NoteVersions, Has.Exactly(3).Items);
        Assert.That((fixture.Diets).Where(diet => diet.IsActive), Has.Exactly(1).Items);
        Assert.That((fixture.Diets).Where(diet => !diet.IsActive), Has.Exactly(1).Items);
        Assert.That(fixture.Plans, Has.Exactly(4).Items);
        Assert.That((fixture.Measurements).Any(measurement => measurement.SourceReportId == null), Is.True);
        var delayed = fixture.Measurements.Single(measurement => measurement.SourceReportId != null && measurement.PersistedAt > fixture.Timeline.RelationshipEndedAt);
        Assert.That(delayed.AcceptedAt, Is.LessThan(fixture.Timeline.RelationshipEndedAt));
        Assert.That(delayed.PersistedAt, Is.GreaterThan(fixture.Timeline.RelationshipEndedAt));
        Assert.That((fixture.Diets).All(diet => diet.StartDate.ToString("O").Length == 10 && (diet.EndDate == null || diet.EndDate.Value.ToString("O").Length == 10)), Is.True);
        Assert.That((fixture.NoteVersions).Where(note => note.VisibleToTrainee), Has.Exactly(1).Items);
        Assert.That((fixture.NoteVersions).Any(note => !note.VisibleToTrainee), Is.True);
        Assert.That((fixture.ShareCodes).Any(code => code.ExpiresAt > fixture.Timeline.ReferenceNow), Is.True);
        Assert.That((fixture.ShareCodes).Any(code => code.ExpiresAt < fixture.Timeline.ReferenceNow), Is.True);
        Assert.That(fixture.Photos, Has.Exactly(2).Items);
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/plans", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/plans", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/plans", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/plans", "trainer-shared", "anonymous-denial")]
    public async Task ManagedPlanList_UsesCanonicalActorsAndRelationshipStates()
    {
        var seeded = await SeedAuthQ01Async();
        var actors = seeded.Actors;
        var fixture = seeded.Definition;
        var traineePlan = seeded.Plans[fixture.Plans.Single(plan => plan.Owner == "U").Id];

        SetAuthorizationHeader(actors["T1"].Id);
        using var allowed = await Client.GetAsync($"/api/trainer/trainees/{actors["U"].Id}/plans");
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var allowedPlans = await allowed.Content.ReadFromJsonAsync<List<ManagedPlanResponse>>();
        Assert.That(allowedPlans, Has.Exactly(1).Items);
        Assert.That(allowedPlans!.Single().Id, Is.EqualTo(traineePlan.Id.ToString()));
        Assert.That(allowedPlans.Single().Name, Is.EqualTo(traineePlan.Name));

        SetAuthorizationHeader(actors["T2"].Id);
        using var unrelated = await Client.GetAsync($"/api/trainer/trainees/{actors["U"].Id}/plans");
        Assert.That(unrelated.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var assertedChain1 = (await unrelated.Content.ReadAsStringAsync());
        Assert.That(assertedChain1, Does.Not.Contain(traineePlan.Id.ToString()));
        Assert.That(assertedChain1, Does.Not.Contain(traineePlan.Name));

        await UnlinkAsync(actors["T1"], actors["U"]);
        SetAuthorizationHeader(actors["T1"].Id);
        using var former = await Client.GetAsync($"/api/trainer/trainees/{actors["U"].Id}/plans");
        Assert.That(former.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var assertedChain2 = (await former.Content.ReadAsStringAsync());
        Assert.That(assertedChain2, Does.Not.Contain(traineePlan.Id.ToString()));
        Assert.That(assertedChain2, Does.Not.Contain(traineePlan.Name));

        ClearAuthorizationHeader();
        using var anonymous = await Client.GetAsync($"/api/trainer/trainees/{actors["U"].Id}/plans");
        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var assertedChain3 = (await anonymous.Content.ReadAsStringAsync());
        Assert.That(assertedChain3, Does.Not.Contain(traineePlan.Id.ToString()));
        Assert.That(assertedChain3, Does.Not.Contain(traineePlan.Name));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/{id}/getPlansList", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/{id}/updatePlan", "own", "foreign-object-denial-no-mutation")]
    public async Task DirectPlanActorMatrix_AllDeclaredActorsPreserveOwnershipAndPayloadBoundaries()
    {
        var seeded = await SeedAuthQ01Async();
        var owner = seeded.Actors["U"];
        var plan = seeded.Plans[seeded.Definition.Plans.Single(item => item.Owner == "U").Id];

        SetAuthorizationHeader(owner.Id);
        using var ownerRead = await Client.GetAsync($"/api/{owner.Id}/getPlansList");
        Assert.That(ownerRead.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerPlans = await ownerRead.Content.ReadFromJsonAsync<List<ManagedPlanResponse>>();
        Assert.That((ownerPlans).Where(item => item.Id == plan.Id.ToString() && item.Name == plan.Name), Has.Exactly(1).Items);

        using var ownerWrite = await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "AUTH-Q01 owner update" });
        Assert.That(ownerWrite.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        foreach (var alias in new[] { "T1", "T2", "T3", "Admin", "AdminTrainer" })
        {
            SetAuthorizationHeader(seeded.Actors[alias].Id);
            using var beforeScope = Factory.Services.CreateScope();
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var before = await beforeDb.Plans.AsNoTracking().SingleAsync(item => item.Id == plan.Id);
            Assert.That(before.Name, Is.EqualTo("AUTH-Q01 owner update"));

            using var read = await Client.GetAsync($"/api/{owner.Id}/getPlansList");
            using var write = await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = $"blocked-{alias}" });
            var readBody = await read.Content.ReadAsStringAsync();
            var writeBody = await write.Content.ReadAsStringAsync();

            Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), alias);
            Assert.That(write.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), alias);
            Assert.That(readBody, Does.Not.Contain(plan.Id.ToString()), alias);
            Assert.That(readBody, Does.Not.Contain("AUTH-Q01 owner update"), alias);
            Assert.That(writeBody, Does.Not.Contain(plan.Id.ToString()), alias);

            using var afterScope = Factory.Services.CreateScope();
            var afterDb = afterScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var after = await afterDb.Plans.AsNoTracking().SingleAsync(item => item.Id == plan.Id);
            Assert.That(after, Is.EqualTo(before).UsingPropertiesComparer());
        }
    }

    [Test]
    public async Task ManagedPlanChain_AccessToBDoesNotGrantAccessToC()
    {
        var seeded = await SeedAuthQ01Async(AuthQ01RelationshipScenario.Chain);
        var a = seeded.Actors["A"];
        var b = seeded.Actors["B"];
        var c = seeded.Actors["C"];
        var bPlan = seeded.Plans[seeded.Definition.Plans.Single(item => item.Owner == "B").Id];
        var cPlan = seeded.Plans[seeded.Definition.Plans.Single(item => item.Owner == "C").Id];

        SetAuthorizationHeader(a.Id);
        using var bResponse = await Client.GetAsync($"/api/trainer/trainees/{b.Id}/plans");
        Assert.That(bResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var bPlans = await bResponse.Content.ReadFromJsonAsync<List<ManagedPlanResponse>>();
        Assert.That(bPlans, Is.Empty);
        var assertedChain4 = (await bResponse.Content.ReadAsStringAsync());
        Assert.That(assertedChain4, Does.Not.Contain(bPlan.Id.ToString()));
        Assert.That(assertedChain4, Does.Not.Contain(bPlan.Name));

        using var cResponse = await Client.GetAsync($"/api/trainer/trainees/{c.Id}/plans");
        Assert.That(cResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var assertedChain5 = (await cResponse.Content.ReadAsStringAsync());
        Assert.That(assertedChain5, Does.Not.Contain(cPlan.Id.ToString()));
        Assert.That(assertedChain5, Does.Not.Contain(cPlan.Name));

        SetAuthorizationHeader(b.Id);
        using var ownerResponse = await Client.GetAsync($"/api/{b.Id}/getPlansList");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerPlans = await ownerResponse.Content.ReadFromJsonAsync<List<ManagedPlanResponse>>();
        Assert.That((ownerPlans).Where(item => item.Id == bPlan.Id.ToString() && item.Name == bPlan.Name), Has.Exactly(1).Items);
    }

    private async Task<AuthQ01SeededFixture> SeedAuthQ01Async(AuthQ01RelationshipScenario scenario = AuthQ01RelationshipScenario.Initial)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var seeded = await AuthQ01Seeder.SeedAsync(db, scenario);
        await unitOfWork.SaveChangesAsync();
        return seeded;
    }

    private async Task UnlinkAsync(User trainer, User trainee)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await db.TrainerTraineeLinks.SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == trainee.Id);
        db.TrainerTraineeLinks.Remove(link);
        await db.SaveChangesAsync();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LgymApi.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for AUTH-Q01 coverage.");
    }

    private sealed class ManagedPlanResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

}
