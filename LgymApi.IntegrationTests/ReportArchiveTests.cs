using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LgymApi.IntegrationTests.Authorization;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ReportArchiveTests : IntegrationTestBase
{
    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
        await db.SaveChangesAsync();
        return trainer;
    }

    [TestCase("?Page=0")]
    [TestCase("?PageSize=0")]
    [TestCase("?PageSize=101")]
    [TestCase("?Page=2147483647")]
    public async Task Archive_RejectsInvalidPagination(string query)
    {
        var trainer = await SeedTrainerAsync("archive-validation", "archive-validation@example.test");
        SetAuthorizationHeader(trainer.Id);
        using var response = await Client.GetAsync("/api/trainer/report-submissions" + query);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/report-submissions", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/report-submissions", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/report-submissions", "trainer-shared", "source-episode-history-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/report-submissions", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainer/report-submissions", "trainer-shared", "anonymous-denial")]
    public async Task Archive_RetainsSourceHistoryAfterUnlink_PaginatesAndExcludesDeletedSubject()
    {
        var trainer = await SeedTrainerAsync("archive-source", "archive-source@example.test");
        var successor = await SeedTrainerAsync("archive-successor", "archive-successor@example.test");
        var trainee = await SeedUserAsync("archive-subject", "archive-subject@example.test");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id
            });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(trainer.Id);
        using var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Frozen archive", fields = new[] { new { key = "note", label = "Original label", type = "Text", isRequired = false, order = 0 } }
        });
        templateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var template = JsonDocument.Parse(await templateResponse.Content.ReadAsStringAsync());
        var templateId = template.RootElement.GetProperty("_id").GetString();
        for (var i = 0; i < 2; i++)
        {
            SetAuthorizationHeader(trainer.Id);
            using var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new { templateId });
            requestResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            using var request = JsonDocument.Parse(await requestResponse.Content.ReadAsStringAsync());
            var requestId = request.RootElement.GetProperty("_id").GetString();
            SetAuthorizationHeader(trainee.Id);
            using var submit = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{requestId}/submit", new { answers = new { note = $"Answer {i}" } });
            submit.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        SetAuthorizationHeader(trainer.Id);
        using var active = await ReadArchive("");
        active.RootElement.GetProperty("items").GetArrayLength().Should().Be(2);
        SetAuthorizationHeader(successor.Id);
        using var unrelated = await ReadArchive("");
        unrelated.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Remove(await db.TrainerTraineeLinks.SingleAsync(x => x.TrainerId == trainer.Id && x.TraineeId == trainee.Id));
            await db.SaveChangesAsync();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(), TrainerId = successor.Id, TraineeId = trainee.Id
            });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(trainer.Id);
        using var deleteTemplate = await Client.PostAsJsonAsync($"/api/trainer/report-templates/{templateId}/delete", new { });
        deleteTemplate.StatusCode.Should().Be(HttpStatusCode.OK);
        using var first = await ReadArchive("?Page=1&PageSize=1");
        first.RootElement.GetProperty("nextPage").GetInt32().Should().Be(2);
        var item = first.RootElement.GetProperty("items")[0];
        item.GetProperty("canEditFeedback").GetBoolean().Should().BeFalse();
        item.GetProperty("request").GetProperty("template").GetProperty("name").GetString().Should().Be("Frozen archive");
        using var second = await ReadArchive("?Page=2&PageSize=1");
        second.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        second.RootElement.GetProperty("items")[0].GetProperty("_id").GetString().Should().NotBe(item.GetProperty("_id").GetString());
        second.RootElement.TryGetProperty("nextPage", out var nextPage).Should().BeFalse();

        SetAuthorizationHeader(successor.Id);
        using var denied = await ReadArchive("");
        denied.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync(x => x.Id == trainee.Id)).IsDeleted = true;
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(trainer.Id);
        using var deleted = await ReadArchive("");
        deleted.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
        Client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await Client.GetAsync("/api/trainer/report-submissions");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<JsonDocument> ReadArchive(string query)
    {
        using var response = await Client.GetAsync("/api/trainer/report-submissions" + query);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
