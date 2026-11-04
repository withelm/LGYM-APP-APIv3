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

        fixture.SchemaVersion.Should().Be(1);
        fixture.FixtureId.Should().Be("AUTH-Q01-v1");
        fixture.SyntheticOnly.Should().BeTrue();
        fixture.Actors.Should().HaveCount(9);
        fixture.Reports.Should().HaveCount(2);
        fixture.NoteVersions.Should().HaveCount(3);
        fixture.Diets.Should().ContainSingle(diet => diet.IsActive);
        fixture.Diets.Should().ContainSingle(diet => !diet.IsActive);
        fixture.Plans.Should().HaveCount(4);
        fixture.Measurements.Should().Contain(measurement => measurement.SourceReportId == null);
        var delayed = fixture.Measurements.Single(measurement => measurement.SourceReportId != null && measurement.PersistedAt > fixture.Timeline.RelationshipEndedAt);
        delayed.AcceptedAt.Should().BeBefore(fixture.Timeline.RelationshipEndedAt);
        delayed.PersistedAt.Should().BeAfter(fixture.Timeline.RelationshipEndedAt);
        fixture.Diets.Should().OnlyContain(diet => diet.StartDate.ToString("O").Length == 10 && (diet.EndDate == null || diet.EndDate.Value.ToString("O").Length == 10));
        fixture.NoteVersions.Should().ContainSingle(note => note.VisibleToTrainee);
        fixture.NoteVersions.Should().Contain(note => !note.VisibleToTrainee);
        fixture.ShareCodes.Should().Contain(code => code.ExpiresAt > fixture.Timeline.ReferenceNow);
        fixture.ShareCodes.Should().Contain(code => code.ExpiresAt < fixture.Timeline.ReferenceNow);
        fixture.Photos.Should().HaveCount(2);
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
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        var allowedPlans = await allowed.Content.ReadFromJsonAsync<List<ManagedPlanResponse>>();
        allowedPlans.Should().ContainSingle();
        allowedPlans!.Single().Id.Should().Be(traineePlan.Id.ToString());
        allowedPlans.Single().Name.Should().Be(traineePlan.Name);

        SetAuthorizationHeader(actors["T2"].Id);
        using var unrelated = await Client.GetAsync($"/api/trainer/trainees/{actors["U"].Id}/plans");
        unrelated.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unrelated.Content.ReadAsStringAsync()).Should().NotContain(traineePlan.Id.ToString()).And.NotContain(traineePlan.Name);

        await UnlinkAsync(actors["T1"], actors["U"]);
        SetAuthorizationHeader(actors["T1"].Id);
        using var former = await Client.GetAsync($"/api/trainer/trainees/{actors["U"].Id}/plans");
        former.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await former.Content.ReadAsStringAsync()).Should().NotContain(traineePlan.Id.ToString()).And.NotContain(traineePlan.Name);

        ClearAuthorizationHeader();
        using var anonymous = await Client.GetAsync($"/api/trainer/trainees/{actors["U"].Id}/plans");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.Content.ReadAsStringAsync()).Should().NotContain(traineePlan.Id.ToString()).And.NotContain(traineePlan.Name);
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
        ownerRead.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerPlans = await ownerRead.Content.ReadFromJsonAsync<List<ManagedPlanResponse>>();
        ownerPlans.Should().ContainSingle(item => item.Id == plan.Id.ToString() && item.Name == plan.Name);

        using var ownerWrite = await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = "AUTH-Q01 owner update" });
        ownerWrite.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var alias in new[] { "T1", "T2", "T3", "Admin", "AdminTrainer" })
        {
            SetAuthorizationHeader(seeded.Actors[alias].Id);
            using var beforeScope = Factory.Services.CreateScope();
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var before = await beforeDb.Plans.AsNoTracking().SingleAsync(item => item.Id == plan.Id);
            before.Name.Should().Be("AUTH-Q01 owner update");

            using var read = await Client.GetAsync($"/api/{owner.Id}/getPlansList");
            using var write = await PostAsJsonWithApiOptionsAsync($"/api/{owner.Id}/updatePlan", new { _id = plan.Id.ToString(), name = $"blocked-{alias}" });
            var readBody = await read.Content.ReadAsStringAsync();
            var writeBody = await write.Content.ReadAsStringAsync();

            read.StatusCode.Should().Be(HttpStatusCode.Forbidden, alias);
            write.StatusCode.Should().Be(HttpStatusCode.Forbidden, alias);
            readBody.Should().NotContain(plan.Id.ToString(), alias);
            readBody.Should().NotContain("AUTH-Q01 owner update", alias);
            writeBody.Should().NotContain(plan.Id.ToString(), alias);

            using var afterScope = Factory.Services.CreateScope();
            var afterDb = afterScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var after = await afterDb.Plans.AsNoTracking().SingleAsync(item => item.Id == plan.Id);
            after.Should().BeEquivalentTo(before);
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
        bResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bPlans = await bResponse.Content.ReadFromJsonAsync<List<ManagedPlanResponse>>();
        bPlans.Should().ContainSingle(item => item.Id == bPlan.Id.ToString() && item.Name == bPlan.Name);

        using var cResponse = await Client.GetAsync($"/api/trainer/trainees/{c.Id}/plans");
        cResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await cResponse.Content.ReadAsStringAsync()).Should().NotContain(cPlan.Id.ToString()).And.NotContain(cPlan.Name);
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
