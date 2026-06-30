using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
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

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<TraineeNoteResponse>();
        created.Should().NotBeNull();
        created!.VisibleToTrainee.Should().BeTrue();

        var updateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/notes/{created.Id}/update", new
        {
            title = "Weekly focus",
            content = "Watch recovery and reduce carbs in the evening.",
            visibleToTrainee = true,
            isPinned = true,
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/notes/{created.Id}/history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<TraineeNoteHistoryResponse>>();
        history.Should().NotBeNull();
        history!.Select(x => x.ChangeType).Should().Contain(new[] { "Created", "Updated" });

        SetAuthorizationHeader(trainee.Id);
        var visibleNotesResponse = await Client.GetAsync("/api/trainee/notes");
        visibleNotesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var visibleNotes = await visibleNotesResponse.Content.ReadFromJsonAsync<List<TraineeNoteResponse>>();
        visibleNotes.Should().ContainSingle(x => x.Id == created.Id);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationCommand = await db.CommandEnvelopes
            .Where(x => x.CommandTypeFullName.Contains("TraineeNoteUpdatedInAppNotificationCommand"))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        notificationCommand.Should().NotBeNull();
        notificationCommand!.PayloadJson.Should().Contain(trainee.Id.ToString());
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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        SetAuthorizationHeader(trainee.Id);
        var notesResponse = await Client.GetAsync("/api/trainee/notes");
        notesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var notes = await notesResponse.Content.ReadFromJsonAsync<List<TraineeNoteResponse>>();
        notes.Should().NotBeNull();
        notes.Should().BeEmpty();
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == trainer.Id && ur.RoleId == RoleSeedDataConfiguration.TrainerRoleSeedId);
        if (!alreadyLinked)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = trainer.Id,
                RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId
            });
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
