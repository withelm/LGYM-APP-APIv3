using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task PendingTrainee_DeniesCurrentAndFormerAuthorNoteHistoryAndMutation(bool formerTrainer)
    {
        var trainee = await SeedUserAsync();
        var trainer = await SeedUserAsync("note-deletion-trainer", "note-deletion-trainer@example.test");
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
