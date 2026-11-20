using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TraineeNotesApiTests : IntegrationTestBase
{
    [Test]
    public async Task TrainerCanCreateUpdateHistoryAndNotifyVisibleNote()
    {
        var trainer = await SeedTrainerAsync("trainer-note", "trainer-note@example.com");
        var trainee = await SeedUserAsync(name: "trainee-note", email: "trainee-note@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var createResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/notes", new
        {
            title = "Weekly focus",
            content = "Watch recovery and keep carbs stable.",
            visibleToTrainee = true,
            isPinned = true,
        });

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<TraineeNoteResponse>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.VisibleToTrainee, Is.True);

        var updateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/notes/{created.Id}/update", new
        {
            title = "Weekly focus",
            content = "Watch recovery and reduce carbs in the evening.",
            visibleToTrainee = true,
            isPinned = true,
        });

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var historyResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/notes/{created.Id}/history");
        Assert.That(historyResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var history = await historyResponse.Content.ReadFromJsonAsync<List<TraineeNoteHistoryResponse>>();
        Assert.That(history, Is.Not.Null);
        Assert.That(history!.Select(x => x.ChangeType), Is.SupersetOf(new[] { "Created", "Updated" }));

        SetAuthorizationHeader(trainee.Id);
        var visibleNotesResponse = await Client.GetAsync("/api/trainee/notes");
        Assert.That(visibleNotesResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var visibleNotes = await visibleNotesResponse.Content.ReadFromJsonAsync<List<TraineeNoteResponse>>();
        Assert.That((visibleNotes).Where(x => x.Id == created.Id), Has.Exactly(1).Items);

        SetAuthorizationHeader(trainer.Id);
        var deleteResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/notes/{created.Id}/delete", null);
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var deleted = JsonDocument.Parse(await deleteResponse.Content.ReadAsStringAsync());
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.That(deleted.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.Deleted));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        SetAuthorizationHeader(trainee.Id);
        var deletedNoteResponse = await Client.GetAsync($"/api/trainee/notes/{created.Id}");
        Assert.That(deletedNoteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationCommand = await db.CommandEnvelopes
            .Where(x => x.CommandTypeFullName.Contains("TraineeNoteUpdatedInAppNotificationCommand"))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.That(notificationCommand, Is.Not.Null);
        Assert.That(notificationCommand!.PayloadJson, Does.Contain(trainee.Id.ToString()));
    }

    [Test]
    public async Task TrainerCannotCreateNoteForForeignTrainee()
    {
        var ownerTrainer = await SeedTrainerAsync("trainer-owner-note", "trainer-owner-note@example.com");
        var otherTrainer = await SeedTrainerAsync("trainer-other-note", "trainer-other-note@example.com");
        var trainee = await SeedUserAsync(name: "trainee-foreign-note", email: "trainee-foreign-note@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(ownerTrainer.Id, trainee.Id);

        SetAuthorizationHeader(otherTrainer.Id);
        var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/notes", new
        {
            content = "Forbidden note",
            visibleToTrainee = true,
            isPinned = false,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task InvisibleNoteIsNotVisibleForTrainee()
    {
        var trainer = await SeedTrainerAsync("trainer-hidden-note", "trainer-hidden-note@example.com");
        var trainee = await SeedUserAsync(name: "trainee-hidden-note", email: "trainee-hidden-note@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var createResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/notes", new
        {
            title = "Private coach note",
            content = "Internal only",
            visibleToTrainee = false,
            isPinned = false,
        });

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        SetAuthorizationHeader(trainee.Id);
        var notesResponse = await Client.GetAsync("/api/trainee/notes");
        Assert.That(notesResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var notes = await notesResponse.Content.ReadFromJsonAsync<List<TraineeNoteResponse>>();
        Assert.That(notes, Is.Not.Null);
        Assert.That(notes, Is.Empty);
    }

    [Test]
    public async Task MalformedNoteIds_PreserveLegacyErrorPrecedenceAndMessages()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var trainer = await SeedTrainerAsync("trainer-malformed-note", "trainer-malformed-note@example.com");
            SetAuthorizationHeader(trainer.Id);

            var malformedTrainee = await Client.GetAsync("/api/trainer/trainees/not-a-guid/notes");
            Assert.That(malformedTrainee.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            using var traineeError = JsonDocument.Parse(await malformedTrainee.Content.ReadAsStringAsync());
            Assert.That(traineeError.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.UserIdRequired));

            var malformedNote = await Client.PostAsJsonAsync(
                $"/api/trainer/trainees/{Id<User>.New()}/notes/not-a-guid/update",
                new { content = "Ignored" });
            Assert.That(malformedNote.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            using var noteError = JsonDocument.Parse(await malformedNote.Content.ReadAsStringAsync());
            Assert.That(noteError.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.FieldRequired));

            var trainee = await SeedUserAsync("trainee-malformed-visible-note", "trainee-malformed-visible-note@example.com", "password123");
            SetAuthorizationHeader(trainee.Id);
            var malformedVisibleNote = await Client.GetAsync("/api/trainee/notes/not-a-guid");
            Assert.That(malformedVisibleNote.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            using var visibleError = JsonDocument.Parse(await malformedVisibleNote.Content.ReadAsStringAsync());
            Assert.That(visibleError.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.FieldRequired));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == trainer.Id && ur.RoleId == RoleSeedDataConfiguration.TrainerRoleSeedId);
        if (!alreadyLinked)
        {
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
            await db.SaveChangesAsync();
        }

        return trainer;
    }

    private async Task LinkTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId
        });
        await db.SaveChangesAsync();
    }

    private sealed class TraineeNoteResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("visibleToTrainee")]
        public bool VisibleToTrainee { get; set; }
    }

    private sealed class TraineeNoteHistoryResponse
    {
        [JsonPropertyName("changeType")]
        public string ChangeType { get; set; } = string.Empty;
    }
}
