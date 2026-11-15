using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Api.Idempotency;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class AccountDeletionTargetAccessTests : IntegrationTestBase
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task InvitationReplay_DeniesPendingSubjectAndReevaluatesAfterRecoveryWithoutDuplicatingInvitation(bool pascalCase, bool activeDecoy)
    {
        var trainee = await SeedUserAsync();
        var trainer = await SeedUserAsync("deletion-replay-trainer", "deletion-replay-trainer@example.test");
        using (var setup = Factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(trainer.Id);
        var trainerToken = Client.DefaultRequestHeaders.Authorization;
        Client.DefaultRequestHeaders.Add(ApiIdempotencyHeaders.IdempotencyKey, "deletion-subject-replay");
        var request = new Dictionary<string, string>();
        if (activeDecoy)
        {
            var decoy = await SeedUserAsync("replay-decoy", "replay-decoy@example.test");
            request.Add("traineeId", decoy.Id.ToString());
        }
        request.Add(pascalCase ? "TraineeId" : "traineeId", trainee.Id.ToString());
        var accepted = await Client.PostAsJsonAsync("/api/trainer/invitations", request);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        var acceptedBody = await accepted.Content.ReadAsStringAsync();
        var initialReplay = await Client.PostAsJsonAsync("/api/trainer/invitations", request);
        initialReplay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await initialReplay.Content.ReadAsStringAsync()).Should().Be(acceptedBody);
        Client.DefaultRequestHeaders.Remove(ApiIdempotencyHeaders.IdempotencyKey);
        SetAuthorizationHeader(trainee.Id);
        (await Client.GetAsync("/api/deleteAccount")).EnsureSuccessStatusCode();
        Client.DefaultRequestHeaders.Authorization = trainerToken;
        Client.DefaultRequestHeaders.Add(ApiIdempotencyHeaders.IdempotencyKey, "deletion-subject-replay");

        var denied = await Client.PostAsJsonAsync("/api/trainer/invitations", request);

        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await denied.Content.ReadAsStringAsync()).Should().NotContain(trainee.Id.ToString());
        Client.DefaultRequestHeaders.Remove(ApiIdempotencyHeaders.IdempotencyKey);
        ClearAuthorizationHeader();
        var recovery = await Client.PostAsJsonAsync("/api/account-deletion/recovery/login", new { name = trainee.Name, password = "password123" });
        recovery.EnsureSuccessStatusCode();
        var token = (await recovery.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryToken").GetString();
        (await Client.PostAsJsonAsync("/api/account-deletion/recovery/cancel", new { recoveryToken = token })).EnsureSuccessStatusCode();
        Client.DefaultRequestHeaders.Authorization = trainerToken;
        Client.DefaultRequestHeaders.Add(ApiIdempotencyHeaders.IdempotencyKey, "deletion-subject-replay");
        var restored = await Client.PostAsJsonAsync("/api/trainer/invitations", request);
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        (await restored.Content.ReadAsStringAsync()).Should().Be(acceptedBody);
        using var verify = Factory.Services.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<AppDbContext>().TrainerInvitations
            .CountAsync(invitation => invitation.TrainerId == trainer.Id && invitation.TraineeId == trainee.Id)).Should().Be(1);
    }

    [Test]
    public async Task PendingTrainee_CannotReceiveAnEmailInvitationAsAnUnknownAccount()
    {
        var trainee = await SeedUserAsync();
        var trainer = await SeedUserAsync("email-deletion-trainer", "email-deletion-trainer@example.test");
        using (var setup = Factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(trainee.Id);
        (await Client.GetAsync("/api/deleteAccount")).EnsureSuccessStatusCode();
        SetAuthorizationHeader(trainer.Id);

        var response = await Client.PostAsJsonAsync("/api/trainer/invitations/by-email", new { email = trainee.Email.Value });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var verify = Factory.Services.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<AppDbContext>().TrainerInvitations.AnyAsync()).Should().BeFalse();
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task PendingTrainee_DeniesCurrentAndFormerAuthorNoteHistoryAndMutation(bool formerTrainer, bool isAdmin)
    {
        var trainee = await SeedUserAsync();
        var trainer = await SeedUserAsync("note-deletion-trainer", "note-deletion-trainer@example.test", isAdmin: isAdmin);
        var noteId = Id<TraineeNote>.New();
        using (var setup = Factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
            if (!formerTrainer)
                db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id });
            db.TraineeNotes.Add(new TraineeNote
            {
                Id = noteId, TrainerId = trainer.Id, TraineeId = trainee.Id,
                LastUpdatedByUserId = trainer.Id, Content = "preserve this private note"
            });
            await db.SaveChangesAsync();
        }
        SetAuthorizationHeader(trainer.Id);
        var trainerToken = Client.DefaultRequestHeaders.Authorization;
        var path = $"/api/trainer/trainees/{trainee.Id}/notes/{noteId}";
        (await Client.GetAsync(path + "/history")).StatusCode.Should().Be(HttpStatusCode.OK);
        SetAuthorizationHeader(trainee.Id);
        (await Client.GetAsync("/api/deleteAccount")).EnsureSuccessStatusCode();
        Client.DefaultRequestHeaders.Authorization = trainerToken;

        (await Client.GetAsync(path + "/history")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PostAsJsonAsync(path + "/update", new { content = "changed", title = "changed", visibleToTrainee = true, isPinned = false }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var verify = Factory.Services.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<AppDbContext>().TraineeNotes.SingleAsync(note => note.Id == noteId))
            .Content.Should().Be("preserve this private note");
    }
}
