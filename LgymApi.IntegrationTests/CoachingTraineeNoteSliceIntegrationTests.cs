using LgymApi.Api.Features.Trainer.Contracts;
using LgymApi.Application.Coaching.TraineeNotes.Create;
using LgymApi.Application.Coaching.TraineeNotes.Delete;
using LgymApi.Application.Coaching.TraineeNotes.History;
using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Coaching.TraineeNotes.TrainerList;
using LgymApi.Application.Coaching.TraineeNotes.Update;
using LgymApi.Application.Coaching.TraineeNotes.VisibleList;
using LgymApi.Application.Coaching.TraineeNotes.VisibleSingle;
using LgymApi.Application.Coaching.TraineeNotes.VisibleHistory;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LgymApi.Resources;
using System.Net;
using System.Net.Http.Json;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class CoachingTraineeNoteSliceIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task TrainerNoteSlices_PreserveListCreateUpdateUnshareDeleteHistoryAndNotificationBehavior()
    {
        var trainer = await SeedUserAsync("slice-note-trainer", "slice-note-trainer@example.test");
        var trainee = await SeedUserAsync("slice-note-trainee", "slice-note-trainee@example.test");
        await SeedRelationshipAsync(trainer.Id, trainee.Id);

        Id<TraineeNote> noteId;
        using (var createScope = Factory.Services.CreateScope())
        {
            var result = await createScope.ServiceProvider.GetRequiredService<ICreateTraineeNoteUseCase>().ExecuteAsync(
                new CreateTraineeNoteCommand(
                    trainer.Id,
                    trainee.Id,
                    new TraineeNoteUpsertData("   ", "  Initial content  ", true, true)));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Title, Is.Null);
            Assert.That(result.Value.Content, Is.EqualTo("Initial content"));
            Assert.That(result.Value.VisibleToTrainee, Is.True);
            Assert.That(result.Value.IsPinned, Is.True);
            noteId = result.Value.Id;
        }

        using (var listScope = Factory.Services.CreateScope())
        {
            var result = await listScope.ServiceProvider.GetRequiredService<IListTrainerNotesUseCase>()
                .ExecuteAsync(new ListTrainerNotesQuery(trainer.Id, trainee.Id));
            Assert.That((result.Value).Where(note => note.Id == noteId), Has.Exactly(1).Items);
        }

        using (var updateScope = Factory.Services.CreateScope())
        {
            var result = await updateScope.ServiceProvider.GetRequiredService<IUpdateTraineeNoteUseCase>().ExecuteAsync(
                new UpdateTraineeNoteCommand(
                    trainer.Id,
                    trainee.Id,
                    noteId,
                    new TraineeNoteUpsertData("  Updated title  ", "  Updated content  ", false, false)));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Title, Is.EqualTo("Updated title"));
            Assert.That(result.Value.Content, Is.EqualTo("Updated content"));
            Assert.That(result.Value.VisibleToTrainee, Is.False);
        }

        using (var historyScope = Factory.Services.CreateScope())
        {
            var result = await historyScope.ServiceProvider.GetRequiredService<IGetTraineeNoteHistoryUseCase>()
                .ExecuteAsync(new GetTraineeNoteHistoryQuery(trainer.Id, trainee.Id, noteId));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Select(entry => entry.ChangeType), Is.EqualTo(new[] { "Updated", "Created" }));
            Assert.That(result.Value.Single(entry => entry.ChangeType == "Updated"), new NUnit.Framework.Constraints.PredicateConstraint<TraineeNoteHistoryReadModel>(entry =>
                entry.PreviousContent == "Initial content" && entry.NewContent == "Updated content"));
        }

        using (var deleteScope = Factory.Services.CreateScope())
        {
            var result = await deleteScope.ServiceProvider.GetRequiredService<IDeleteTraineeNoteUseCase>()
                .ExecuteAsync(new DeleteTraineeNoteCommand(trainer.Id, trainee.Id, noteId));
            Assert.That(result.IsSuccess, Is.True);
        }

        using var verificationScope = Factory.Services.CreateScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await database.TraineeNotes.IgnoreQueryFilters().SingleAsync(note => note.Id == noteId);
        Assert.That(persisted.IsDeleted, Is.True);
        Assert.That(persisted.VisibleToTrainee, Is.False);
        Assert.That(persisted.Content, Is.EqualTo("Updated content"));
        Assert.That(persisted.LastUpdatedByUserId, Is.EqualTo(trainer.Id));
        var history = await database.TraineeNoteHistories
            .Where(entry => entry.TraineeNoteId == noteId)
            .OrderBy(entry => entry.ChangedAt)
            .ToListAsync();
        Assert.That(history.Select(entry => entry.ChangeType), Is.EqualTo(new[] { "Created", "Updated", "Deleted" }));
        Assert.That(history.Last().PreviousContent, Is.EqualTo("Updated content"));
        Assert.That(history.Last().NewContent, Is.EqualTo("Updated content"));
        Assert.That((await database.CommandEnvelopes.CountAsync(envelope =>
            envelope.CommandTypeFullName.Contains("TraineeNoteUpdatedInAppNotificationCommand"))), Is.EqualTo(1));
    }

    [Test]
    public async Task TrainerNoteSlices_RejectForeignRelationshipForeignNoteEmptyContentAndInvalidNoteId()
    {
        var trainer = await SeedUserAsync("slice-note-access-trainer", "slice-note-access-trainer@example.test");
        var otherTrainer = await SeedUserAsync("slice-note-access-other", "slice-note-access-other@example.test");
        var trainee = await SeedUserAsync("slice-note-access-trainee", "slice-note-access-trainee@example.test");
        await SeedTrainerRoleAsync(trainer.Id);
        await SeedTrainerRoleAsync(otherTrainer.Id);

        using (var foreignRelationshipScope = Factory.Services.CreateScope())
        {
            var result = await foreignRelationshipScope.ServiceProvider.GetRequiredService<ICreateTraineeNoteUseCase>().ExecuteAsync(
                new CreateTraineeNoteCommand(
                    trainer.Id,
                    trainee.Id,
                    new TraineeNoteUpsertData(null, "Foreign", false, false)));
            Assert.That(result.Error, Is.TypeOf<NotFoundError>());
        }

        await SeedRelationshipAsync(trainer.Id, trainee.Id, addTrainerRole: false);
        Id<TraineeNote> foreignNoteId;
        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var foreignNote = new TraineeNote
            {
                Id = Id<TraineeNote>.New(),
                TrainerId = otherTrainer.Id,
                TraineeId = trainee.Id,
                Content = "Other trainer",
                LastUpdatedByUserId = otherTrainer.Id,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            database.TraineeNotes.Add(foreignNote);
            await database.SaveChangesAsync();
            foreignNoteId = foreignNote.Id;
        }

        using var actionScope = Factory.Services.CreateScope();
        var services = actionScope.ServiceProvider;
        var foreignNoteResult = await services.GetRequiredService<IUpdateTraineeNoteUseCase>().ExecuteAsync(
            new UpdateTraineeNoteCommand(
                trainer.Id,
                trainee.Id,
                foreignNoteId,
                new TraineeNoteUpsertData(null, "Nope", false, false)));
        var emptyContent = await services.GetRequiredService<ICreateTraineeNoteUseCase>().ExecuteAsync(
            new CreateTraineeNoteCommand(
                trainer.Id,
                trainee.Id,
                new TraineeNoteUpsertData(null, " ", false, false)));
        var invalidNoteId = await services.GetRequiredService<IDeleteTraineeNoteUseCase>()
            .ExecuteAsync(new DeleteTraineeNoteCommand(trainer.Id, trainee.Id, Id<TraineeNote>.Empty));

        Assert.That(foreignNoteResult.Error, Is.TypeOf<NotFoundError>());
        Assert.That(emptyContent.Error, Is.TypeOf<BadRequestError>());
        Assert.That(invalidNoteId.Error, Is.TypeOf<BadRequestError>());
        Assert.That((await services.GetRequiredService<AppDbContext>().TraineeNoteHistories.CountAsync()), Is.EqualTo(0));
    }

    [Test]
    public async Task TraineeVisibleNoteSlices_PreserveOwnershipVisibilityGlobalFilterOrderAndNoTrackingReads()
    {
        var trainer = await SeedUserAsync("visible-note-trainer", "visible-note-trainer@example.test");
        var trainee = await SeedUserAsync("visible-note-trainee", "visible-note-trainee@example.test");
        var otherTrainee = await SeedUserAsync("visible-note-other", "visible-note-other@example.test");
        var baseline = DateTimeOffset.UtcNow.AddHours(-1);
        var pinned = VisibleNote(trainer.Id, trainee.Id, "Pinned", baseline, isPinned: true);
        var recent = VisibleNote(trainer.Id, trainee.Id, "Recent", baseline.AddMinutes(20));
        var older = VisibleNote(trainer.Id, trainee.Id, "Older", baseline.AddMinutes(10));
        var privateNote = VisibleNote(trainer.Id, trainee.Id, "Private", baseline.AddMinutes(30), visible: false);
        var deleted = VisibleNote(trainer.Id, trainee.Id, "Deleted", baseline.AddMinutes(40), isDeleted: true);
        var foreign = VisibleNote(trainer.Id, otherTrainee.Id, "Foreign", baseline.AddMinutes(50));

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.AddRange(pinned, recent, older, privateNote, deleted, foreign);
            await database.SaveChangesAsync();
        }

        using var readScope = Factory.Services.CreateScope();
        var services = readScope.ServiceProvider;
        var list = await services.GetRequiredService<IListVisibleTraineeNotesUseCase>()
            .ExecuteAsync(new ListVisibleTraineeNotesQuery(trainee.Id));
        var detail = services.GetRequiredService<IGetVisibleTraineeNoteUseCase>();
        var visibleResult = await detail.ExecuteAsync(new GetVisibleTraineeNoteQuery(trainee.Id, recent.Id));
        var privateResult = await detail.ExecuteAsync(new GetVisibleTraineeNoteQuery(trainee.Id, privateNote.Id));
        var deletedResult = await detail.ExecuteAsync(new GetVisibleTraineeNoteQuery(trainee.Id, deleted.Id));
        var foreignResult = await detail.ExecuteAsync(new GetVisibleTraineeNoteQuery(trainee.Id, foreign.Id));
        var missingResult = await detail.ExecuteAsync(
            new GetVisibleTraineeNoteQuery(trainee.Id, Id<TraineeNote>.New()));
        var invalidResult = await detail.ExecuteAsync(
            new GetVisibleTraineeNoteQuery(trainee.Id, Id<TraineeNote>.Empty));

        Assert.That(list.IsSuccess, Is.True);
        Assert.That(list.Value.Select(note => note.Id), Is.EqualTo(new[] { pinned.Id, recent.Id, older.Id }));
        Assert.That(visibleResult.IsSuccess, Is.True);
        Assert.That(visibleResult.Value.Content, Is.EqualTo("Recent"));
        Assert.That(privateResult.Error, Is.TypeOf<NotFoundError>());
        Assert.That(deletedResult.Error, Is.TypeOf<NotFoundError>());
        Assert.That(foreignResult.Error, Is.TypeOf<NotFoundError>());
        Assert.That(missingResult.Error, Is.TypeOf<NotFoundError>());
        Assert.That(missingResult.Error.Message, Is.EqualTo(Messages.DidntFind));
        Assert.That(invalidResult.Error, Is.TypeOf<BadRequestError>());
        Assert.That(invalidResult.Error.Message, Is.EqualTo(Messages.FieldRequired));

        var readDatabase = services.GetRequiredService<AppDbContext>();
        Assert.That(readDatabase.ChangeTracker.Entries(), Is.Empty);
        Assert.That((await readDatabase.TraineeNotes.IgnoreQueryFilters().CountAsync(note =>
            note.Id == pinned.Id
            || note.Id == recent.Id
            || note.Id == older.Id
            || note.Id == privateNote.Id
            || note.Id == deleted.Id
            || note.Id == foreign.Id)), Is.EqualTo(6));
    }

    [Test]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes", "own", "owner-allow")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes", "own", "no-client-subject")]
    public async Task TraineeNotesRoute_ReturnsOnlyVisibleNotesForAuthenticatedActor()
    {
        var trainer = await SeedUserAsync("http-note-trainer", "http-note-trainer@example.test");
        var trainee = await SeedUserAsync("http-note-trainee", "http-note-trainee@example.test");
        var otherTrainee = await SeedUserAsync("http-note-other", "http-note-other@example.test");
        var visible = VisibleNote(trainer.Id, trainee.Id, "HTTP visible", DateTimeOffset.UtcNow);
        var hidden = VisibleNote(trainer.Id, trainee.Id, "HTTP hidden", DateTimeOffset.UtcNow, visible: false);
        var deleted = VisibleNote(trainer.Id, trainee.Id, "HTTP deleted", DateTimeOffset.UtcNow, isDeleted: true);
        var foreign = VisibleNote(trainer.Id, otherTrainee.Id, "HTTP foreign", DateTimeOffset.UtcNow);

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.AddRange(visible, hidden, deleted, foreign);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        using var response = await Client.GetAsync("/api/trainee/notes");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<List<TraineeNoteDto>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Select(note => note.Id), Is.EqualTo(new[] { visible.Id.ToString() }));
        Assert.That(body.Single().Content, Is.EqualTo("HTTP visible"));
        var responseText = await response.Content.ReadAsStringAsync();
        Assert.That(responseText, Does.Not.Contain(hidden.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(hidden.Content));
        Assert.That(responseText, Does.Not.Contain(deleted.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(deleted.Content));
        Assert.That(responseText, Does.Not.Contain(foreign.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(foreign.Content));
    }

    [Test]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes", "own", "anonymous-denial")]
    public async Task TraineeNotesRoute_WithoutAuthentication_ReturnsUnauthorizedWithoutProtectedContent()
    {
        var trainer = await SeedUserAsync("http-anonymous-note-trainer", "http-anonymous-note-trainer@example.test");
        var trainee = await SeedUserAsync("http-anonymous-note-trainee", "http-anonymous-note-trainee@example.test");
        var visible = VisibleNote(trainer.Id, trainee.Id, "HTTP anonymous protected", DateTimeOffset.UtcNow);

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.Add(visible);
            await database.SaveChangesAsync();
        }

        ClearAuthorizationHeader();
        using var response = await Client.GetAsync("/api/trainee/notes");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var responseText = await response.Content.ReadAsStringAsync();
        Assert.That(responseText, Does.Not.Contain(visible.Content));
        Assert.That(responseText, Does.Not.Contain(visible.Id.ToString()));
    }

    [Test]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes/{noteId}", "own", "owner-allow")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes/{noteId}", "own", "no-client-subject")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes/{noteId}", "own", "foreign-object-denial-no-mutation")]
    public async Task TraineeNoteDetailRoute_ReturnsVisibleNoteAndHidesProtectedNotes()
    {
        var trainer = await SeedUserAsync("http-detail-note-trainer", "http-detail-note-trainer@example.test");
        var trainee = await SeedUserAsync("http-detail-note-trainee", "http-detail-note-trainee@example.test");
        var otherTrainee = await SeedUserAsync("http-detail-note-other", "http-detail-note-other@example.test");
        var visible = VisibleNote(trainer.Id, trainee.Id, "HTTP detail visible", DateTimeOffset.UtcNow);
        var hidden = VisibleNote(trainer.Id, trainee.Id, "HTTP detail hidden", DateTimeOffset.UtcNow, visible: false);
        var deleted = VisibleNote(trainer.Id, trainee.Id, "HTTP detail deleted", DateTimeOffset.UtcNow, isDeleted: true);
        var foreign = VisibleNote(trainer.Id, otherTrainee.Id, "HTTP detail foreign", DateTimeOffset.UtcNow);

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.AddRange(visible, hidden, deleted, foreign);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        using var visibleResponse = await Client.GetAsync($"/api/trainee/notes/{visible.Id}");
        Assert.That(visibleResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var visibleBody = await visibleResponse.Content.ReadFromJsonAsync<TraineeNoteDto>();
        Assert.That(visibleBody, Is.Not.Null);
        Assert.That(visibleBody!.Id, Is.EqualTo(visible.Id.ToString()));
        Assert.That(visibleBody.Content, Is.EqualTo(visible.Content));

        foreach (var protectedNote in new[] { hidden, deleted, foreign })
        {
            using var deniedResponse = await Client.GetAsync($"/api/trainee/notes/{protectedNote.Id}");
            Assert.That(deniedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var deniedText = await deniedResponse.Content.ReadAsStringAsync();
            Assert.That(deniedText, Does.Not.Contain(protectedNote.Id.ToString()));
            Assert.That(deniedText, Does.Not.Contain(protectedNote.Content));
        }
    }

    [Test]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes/{noteId}", "own", "anonymous-denial")]
    public async Task TraineeNoteDetailRoute_WithoutAuthentication_ReturnsUnauthorizedWithoutProtectedContent()
    {
        var trainer = await SeedUserAsync("http-anonymous-detail-trainer", "http-anonymous-detail-trainer@example.test");
        var trainee = await SeedUserAsync("http-anonymous-detail-trainee", "http-anonymous-detail-trainee@example.test");
        var visible = VisibleNote(trainer.Id, trainee.Id, "HTTP anonymous detail protected", DateTimeOffset.UtcNow);

        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.Add(visible);
            await database.SaveChangesAsync();
        }

        ClearAuthorizationHeader();
        using var response = await Client.GetAsync($"/api/trainee/notes/{visible.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var responseText = await response.Content.ReadAsStringAsync();
        Assert.That(responseText, Does.Not.Contain(visible.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(visible.Content));
    }

    [Test]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "active-relationship-allow")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "unrelated-relationship-denial")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "former-author-allow")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "foreign-object-denial-no-mutation")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "anonymous-denial")]
    public async Task TrainerTraineeNotesRoute_AllowsOriginalAuthorAfterUnlinkAndRejectsUnrelatedActors()
    {
        var trainer = await SeedUserAsync("http-trainer-notes-trainer", "http-trainer-notes-trainer@example.test");
        var linked = await SeedUserAsync("http-trainer-notes-linked", "http-trainer-notes-linked@example.test");
        var unrelated = await SeedUserAsync("http-trainer-notes-unrelated", "http-trainer-notes-unrelated@example.test");
        var ordinaryUser = await SeedUserAsync("http-trainer-notes-ordinary", "http-trainer-notes-ordinary@example.test");
        var note = VisibleNote(trainer.Id, linked.Id, "HTTP trainer note protected", DateTimeOffset.UtcNow);

        await SeedRelationshipAsync(trainer.Id, linked.Id);
        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.Add(note);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var ownerResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/notes");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerNotes = await ownerResponse.Content.ReadFromJsonAsync<List<TraineeNoteDto>>();
        Assert.That(ownerNotes, Is.Not.Null);
        Assert.That((ownerNotes!).Where(item => item.Id == note.Id.ToString() && item.Content == note.Content), Has.Exactly(1).Items);
        Assert.That(ownerNotes.Single().OriginalAuthorId, Is.EqualTo(trainer.Id.ToString()));

        using var unrelatedResponse = await Client.GetAsync($"/api/trainer/trainees/{unrelated.Id}/notes");
        Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var unrelatedText = await unrelatedResponse.Content.ReadAsStringAsync();
        Assert.That(unrelatedText, Does.Not.Contain(note.Id.ToString()));
        Assert.That(unrelatedText, Does.Not.Contain(note.Content));

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == linked.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        using var formerResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/notes");
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var formerNotes = await formerResponse.Content.ReadFromJsonAsync<List<TraineeNoteDto>>();
        Assert.That((formerNotes).Where(item => item.Id == note.Id.ToString() && item.Content == note.Content), Has.Exactly(1).Items);

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/notes");
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var ordinaryText = await ordinaryResponse.Content.ReadAsStringAsync();
        Assert.That(ordinaryText, Does.Not.Contain(note.Id.ToString()));
        Assert.That(ordinaryText, Does.Not.Contain(note.Content));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/notes");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousText, Does.Not.Contain(note.Id.ToString()));
        Assert.That(anonymousText, Does.Not.Contain(note.Content));
    }

    [Test]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "active-relationship-allow")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "unrelated-relationship-denial")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "former-relationship-denial")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "foreign-object-denial-no-mutation")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "anonymous-denial")]
    public async Task TrainerTraineeNoteCreateRoute_IsRelationshipScopedAndNonDisclosing()
    {
        var trainer = await SeedUserAsync("http-create-note-trainer", "http-create-note-trainer@example.test");
        var linked = await SeedUserAsync("http-create-note-linked", "http-create-note-linked@example.test");
        var unrelated = await SeedUserAsync("http-create-note-unrelated", "http-create-note-unrelated@example.test");
        var ordinaryUser = await SeedUserAsync("http-create-note-ordinary", "http-create-note-ordinary@example.test");
        await SeedRelationshipAsync(trainer.Id, linked.Id);

        SetAuthorizationHeader(trainer.Id);
        using var ownerResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes", new
        {
            title = "HTTP created note",
            content = "HTTP created protected content",
            visibleToTrainee = true,
            isPinned = false
        });
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var ownerText = await ownerResponse.Content.ReadAsStringAsync();
        Assert.That(ownerText, Does.Contain("HTTP created protected content"));

        using var unrelatedResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{unrelated.Id}/notes", new
        {
            title = "HTTP unrelated note",
            content = "HTTP unrelated protected content",
            visibleToTrainee = true,
            isPinned = false
        });
        Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await unrelatedResponse.Content.ReadAsStringAsync()), Does.Not.Contain("HTTP unrelated protected content"));

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == linked.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        using var formerResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes", new
        {
            title = "HTTP former note",
            content = "HTTP former protected content",
            visibleToTrainee = true,
            isPinned = false
        });
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await formerResponse.Content.ReadAsStringAsync()), Does.Not.Contain("HTTP former protected content"));

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes", new
        {
            title = "HTTP ordinary note",
            content = "HTTP ordinary protected content",
            visibleToTrainee = true,
            isPinned = false
        });
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await ordinaryResponse.Content.ReadAsStringAsync()), Does.Not.Contain("HTTP ordinary protected content"));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes", new
        {
            title = "HTTP anonymous note",
            content = "HTTP anonymous protected content",
            visibleToTrainee = true,
            isPinned = false
        });
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anonymousResponse.Content.ReadAsStringAsync()), Does.Not.Contain("HTTP anonymous protected content"));
    }

    [Test]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/delete", "trainer-shared", "active-relationship-allow")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/delete", "trainer-shared", "unrelated-relationship-denial")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/delete", "trainer-shared", "former-author-allow")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/delete", "trainer-shared", "foreign-object-denial-no-mutation")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/delete", "trainer-shared", "anonymous-denial")]
    public async Task TrainerTraineeNoteDeleteRoute_AllowsOriginalAuthorAfterUnlinkAndRejectsUnrelatedActors()
    {
        var trainer = await SeedUserAsync("http-delete-note-trainer", "http-delete-note-trainer@example.test");
        var linked = await SeedUserAsync("http-delete-note-linked", "http-delete-note-linked@example.test");
        var unrelated = await SeedUserAsync("http-delete-note-unrelated", "http-delete-note-unrelated@example.test");
        var ordinaryUser = await SeedUserAsync("http-delete-note-ordinary", "http-delete-note-ordinary@example.test");
        await SeedRelationshipAsync(trainer.Id, linked.Id);

        SetAuthorizationHeader(trainer.Id);
        using var createResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes", new
        {
            title = "HTTP delete note",
            content = "HTTP delete protected content",
            visibleToTrainee = true,
            isPinned = false
        });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<TraineeNoteDto>();
        Assert.That(created, Is.Not.Null);

        using var protectedCreateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes", new
        {
            title = "HTTP protected note",
            content = "HTTP delete protected content",
            visibleToTrainee = true,
            isPinned = false
        });
        Assert.That(protectedCreateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var protectedNote = await protectedCreateResponse.Content.ReadFromJsonAsync<TraineeNoteDto>();
        Assert.That(protectedNote, Is.Not.Null);

        using var ownerResponse = await Client.PostAsync($"/api/trainer/trainees/{linked.Id}/notes/{created!.Id}/delete", null);
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var unrelatedResponse = await Client.PostAsync($"/api/trainer/trainees/{unrelated.Id}/notes/{protectedNote!.Id}/delete", null);
        Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await unrelatedResponse.Content.ReadAsStringAsync()), Does.Not.Contain("HTTP delete protected content"));

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == linked.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        using var formerResponse = await Client.PostAsync($"/api/trainer/trainees/{linked.Id}/notes/{protectedNote.Id}/delete", null);
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.PostAsync($"/api/trainer/trainees/{linked.Id}/notes/{protectedNote.Id}/delete", null);
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.PostAsync($"/api/trainer/trainees/{linked.Id}/notes/{protectedNote.Id}/delete", null);
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/update", "trainer-shared", "active-relationship-allow")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/update", "trainer-shared", "unrelated-relationship-denial")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/update", "trainer-shared", "former-author-allow")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/update", "trainer-shared", "foreign-object-denial-no-mutation")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/update", "trainer-shared", "anonymous-denial")]
    public async Task TrainerTraineeNoteUpdateRoute_AllowsOriginalAuthorAfterUnlinkAndRejectsUnrelatedActors()
    {
        var trainer = await SeedUserAsync("http-update-note-trainer", "http-update-note-trainer@example.test");
        var linked = await SeedUserAsync("http-update-note-linked", "http-update-note-linked@example.test");
        var unrelated = await SeedUserAsync("http-update-note-unrelated", "http-update-note-unrelated@example.test");
        var ordinaryUser = await SeedUserAsync("http-update-note-ordinary", "http-update-note-ordinary@example.test");
        await SeedRelationshipAsync(trainer.Id, linked.Id);

        SetAuthorizationHeader(trainer.Id);
        using var createResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes", new
        {
            title = "HTTP update note",
            content = "HTTP original protected content",
            visibleToTrainee = true,
            isPinned = false
        });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<TraineeNoteDto>();
        Assert.That(created, Is.Not.Null);
        var updatePayload = new
        {
            title = "HTTP updated note",
            content = "HTTP updated protected content",
            visibleToTrainee = false,
            isPinned = true
        };

        using var ownerResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes/{created!.Id}/update", updatePayload);
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await ownerResponse.Content.ReadAsStringAsync()), Does.Contain("HTTP updated protected content"));

        using var unrelatedResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{unrelated.Id}/notes/{created.Id}/update", updatePayload);
        Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await unrelatedResponse.Content.ReadAsStringAsync()), Does.Not.Contain("HTTP updated protected content"));

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == linked.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        using var formerResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes/{created.Id}/update", updatePayload);
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes/{created.Id}/update", updatePayload);
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{linked.Id}/notes/{created.Id}/update", updatePayload);
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes/{noteId}/history", "trainer-shared", "active-relationship-allow")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes/{noteId}/history", "trainer-shared", "unrelated-relationship-denial")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes/{noteId}/history", "trainer-shared", "former-author-allow")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes/{noteId}/history", "trainer-shared", "foreign-object-denial-no-mutation")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes/{noteId}/history", "trainer-shared", "anonymous-denial")]
    public async Task TrainerTraineeNoteHistoryRoute_AllowsOriginalAuthorAfterUnlinkAndRejectsUnrelatedActors()
    {
        var trainer = await SeedUserAsync("http-history-trainer", "http-history-trainer@example.test");
        var linked = await SeedUserAsync("http-history-linked", "http-history-linked@example.test");
        var unrelated = await SeedUserAsync("http-history-unrelated", "http-history-unrelated@example.test");
        var ordinaryUser = await SeedUserAsync("http-history-ordinary", "http-history-ordinary@example.test");
        var note = VisibleNote(trainer.Id, linked.Id, "HTTP history note", DateTimeOffset.UtcNow);
        var history = new TraineeNoteHistory
        {
            Id = Id<TraineeNoteHistory>.New(),
            TraineeNoteId = note.Id,
            ChangedByUserId = trainer.Id,
            ChangedAt = DateTimeOffset.UtcNow,
            PreviousContent = "Before HTTP history",
            NewContent = "After HTTP history",
            ChangeType = "Updated"
        };

        await SeedRelationshipAsync(trainer.Id, linked.Id);
        using (var seedScope = Factory.Services.CreateScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.Add(note);
            database.TraineeNoteHistories.Add(history);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        using var ownerResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/notes/{note.Id}/history");
        Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ownerHistory = await ownerResponse.Content.ReadFromJsonAsync<List<TraineeNoteHistoryDto>>();
        Assert.That(ownerHistory, Is.Not.Null);
        Assert.That((ownerHistory!).Where(item => item.NewContent == history.NewContent && item.ChangeType == history.ChangeType), Has.Exactly(1).Items);

        using var unrelatedResponse = await Client.GetAsync($"/api/trainer/trainees/{unrelated.Id}/notes/{note.Id}/history");
        Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var unrelatedText = await unrelatedResponse.Content.ReadAsStringAsync();
        Assert.That(unrelatedText, Does.Not.Contain(note.Id.ToString()));
        Assert.That(unrelatedText, Does.Not.Contain(history.NewContent));

        using (var unlinkScope = Factory.Services.CreateScope())
        {
            var database = unlinkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks
                .SingleAsync(candidate => candidate.TrainerId == trainer.Id && candidate.TraineeId == linked.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        using var formerResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/notes/{note.Id}/history");
        Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var formerHistory = await formerResponse.Content.ReadFromJsonAsync<List<TraineeNoteHistoryDto>>();
        Assert.That((formerHistory).Where(item => item.NewContent == history.NewContent && item.ChangeType == history.ChangeType), Has.Exactly(1).Items);

        SetAuthorizationHeader(ordinaryUser.Id);
        using var ordinaryResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/notes/{note.Id}/history");
        Assert.That(ordinaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var ordinaryText = await ordinaryResponse.Content.ReadAsStringAsync();
        Assert.That(ordinaryText, Does.Not.Contain(note.Id.ToString()));
        Assert.That(ordinaryText, Does.Not.Contain(history.NewContent));

        ClearAuthorizationHeader();
        using var anonymousResponse = await Client.GetAsync($"/api/trainer/trainees/{linked.Id}/notes/{note.Id}/history");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var anonymousText = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.That(anonymousText, Does.Not.Contain(note.Id.ToString()));
        Assert.That(anonymousText, Does.Not.Contain(history.NewContent));
    }

    [Test]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes/{noteId}/history", "own", "owner-allow")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes/{noteId}/history", "own", "foreign-object-denial-no-mutation")]
    public async Task TraineeNoteHistoryRoute_ReturnsOnlySharedVersionsRedactsPrivatePredecessorsAndRevokesStaleIds()
    {
        var trainer = await SeedUserAsync("history-author", "history-author@example.test");
        var trainee = await SeedUserAsync("history-trainee", "history-trainee@example.test");
        var foreignTrainee = await SeedUserAsync("history-foreign", "history-foreign@example.test");
        var note = VisibleNote(trainer.Id, trainee.Id, "Shared again", DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var history = new[]
        {
            History(note, trainer.Id, "", "Initially shared", false, true, "Created", now.AddMinutes(-3)),
            History(note, trainer.Id, "Initially shared", "Private revision", true, false, "Updated", now.AddMinutes(-2)),
            History(note, trainer.Id, "Private revision", "Shared again", false, true, "Updated", now.AddMinutes(-1))
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.Add(note);
            database.TraineeNoteHistories.AddRange(history);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        using var sharedResponse = await Client.GetAsync($"/api/trainee/notes/{note.Id}/history");
        Assert.That(sharedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var visibleHistory = await sharedResponse.Content.ReadFromJsonAsync<List<TraineeNoteHistoryDto>>();
        Assert.That(visibleHistory, Has.Exactly(2).Items);
        Assert.That(visibleHistory!.Select(entry => entry.NewContent), Is.EquivalentTo(new[] { "Initially shared", "Shared again" }));
        Assert.That(visibleHistory.Single(entry => entry.NewContent == "Shared again").PreviousContent, Is.Null);
        Assert.That((await sharedResponse.Content.ReadAsStringAsync()), Does.Not.Contain("Private revision"));

        SetAuthorizationHeader(foreignTrainee.Id);
        using var foreignResponse = await Client.GetAsync($"/api/trainee/notes/{note.Id}/history");
        Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var foreignText = await foreignResponse.Content.ReadAsStringAsync();
        Assert.That(foreignText, Does.Not.Contain(note.Id.ToString()));
        Assert.That(foreignText, Does.Not.Contain("Initially shared"));

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tracked = await database.TraineeNotes.SingleAsync(candidate => candidate.Id == note.Id);
            tracked.VisibleToTrainee = false;
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        using var hiddenResponse = await Client.GetAsync($"/api/trainee/notes/{note.Id}/history");
        Assert.That(hiddenResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await hiddenResponse.Content.ReadAsStringAsync()), Does.Not.Contain("Initially shared"));
    }

    [Test]
    public async Task TraineeNoteHistoryRoute_ConcurrentReadsAfterDeletionDoNotDiscloseStaleHistory()
    {
        var trainer = await SeedUserAsync("concurrent-history-author", "concurrent-history-author@example.test");
        var trainee = await SeedUserAsync("concurrent-history-trainee", "concurrent-history-trainee@example.test");
        var note = VisibleNote(trainer.Id, trainee.Id, "Concurrent shared", DateTimeOffset.UtcNow);
        var history = History(note, trainer.Id, "", "Concurrent shared", false, true, "Created", DateTimeOffset.UtcNow);
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.Add(note);
            database.TraineeNoteHistories.Add(history);
            await database.SaveChangesAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tracked = await database.TraineeNotes.SingleAsync(candidate => candidate.Id == note.Id);
            tracked.IsDeleted = true;
            tracked.VisibleToTrainee = false;
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainee.Id);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Client.GetAsync($"/api/trainee/notes/{note.Id}/history")));
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That((await response.Content.ReadAsStringAsync()), Does.Not.Contain("Concurrent shared"));
            }
        }
    }

    [Test]
    [Authorization.AuthorizationEvidence("GET", "/api/trainee/notes/{noteId}/history", "own", "anonymous-denial")]
    public async Task TraineeNoteHistoryRoute_WithoutAuthentication_ReturnsUnauthorizedWithoutProtectedContent()
    {
        var trainer = await SeedUserAsync("history-anonymous-author", "history-anonymous-author@example.test");
        var trainee = await SeedUserAsync("history-anonymous-trainee", "history-anonymous-trainee@example.test");
        var note = VisibleNote(trainer.Id, trainee.Id, "Anonymous history protected", DateTimeOffset.UtcNow);
        var history = History(note, trainer.Id, "", "Anonymous history protected", false, true, "Created", DateTimeOffset.UtcNow);

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.Add(note);
            database.TraineeNoteHistories.Add(history);
            await database.SaveChangesAsync();
        }

        ClearAuthorizationHeader();
        using var response = await Client.GetAsync($"/api/trainee/notes/{note.Id}/history");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var responseText = await response.Content.ReadAsStringAsync();
        Assert.That(responseText, Does.Not.Contain(note.Id.ToString()));
        Assert.That(responseText, Does.Not.Contain(history.NewContent));
    }

    [Test]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes", "trainer-shared", "active-successor-denial")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/update", "trainer-shared", "active-successor-denial")]
    [Authorization.AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/notes/{noteId}/delete", "trainer-shared", "active-successor-denial")]
    [Authorization.AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/notes/{noteId}/history", "trainer-shared", "active-successor-denial")]
    public async Task ActiveSuccessorCannotAccessOriginalAuthorsNoteAfterUnlink()
    {
        var originalAuthor = await SeedUserAsync("successor-original", "successor-original@example.test");
        var successor = await SeedUserAsync("successor-active", "successor-active@example.test");
        var trainee = await SeedUserAsync("successor-trainee", "successor-trainee@example.test");
        var note = VisibleNote(originalAuthor.Id, trainee.Id, "Original author content", DateTimeOffset.UtcNow, isPinned: false);
        var history = History(note, originalAuthor.Id, "", note.Content, false, true, "Created", DateTimeOffset.UtcNow);

        await SeedRelationshipAsync(originalAuthor.Id, trainee.Id);
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.TraineeNotes.Add(note);
            database.TraineeNoteHistories.Add(history);
            await database.SaveChangesAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks.SingleAsync(candidate =>
                candidate.TrainerId == originalAuthor.Id && candidate.TraineeId == trainee.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
        }

        await SeedRelationshipAsync(successor.Id, trainee.Id);
        SetAuthorizationHeader(successor.Id);

        using var listResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/notes");
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var listedNotes = await listResponse.Content.ReadFromJsonAsync<List<TraineeNoteDto>>();
        Assert.That(listedNotes, Is.Not.Null);
        Assert.That((listedNotes!).Any(item => item.Id == note.Id.ToString()), Is.False);
        var listText = await listResponse.Content.ReadAsStringAsync();
        Assert.That(listText, Does.Not.Contain(note.Content));

        var updatePayload = new
        {
            title = "Successor mutation",
            content = "Successor mutation content",
            visibleToTrainee = false,
            isPinned = true
        };
        using var updateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/notes/{note.Id}/update", updatePayload);
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await updateResponse.Content.ReadAsStringAsync()), Does.Not.Contain(updatePayload.content));

        using var deleteResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/notes/{note.Id}/delete", null);
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        using var historyResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/notes/{note.Id}/history");
        Assert.That(historyResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var historyText = await historyResponse.Content.ReadAsStringAsync();
        Assert.That(historyText, Does.Not.Contain(note.Id.ToString()));
        Assert.That(historyText, Does.Not.Contain(history.NewContent));

        using var verificationScope = Factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await verificationDatabase.TraineeNotes.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == note.Id);
        Assert.That(persisted.Content, Is.EqualTo(note.Content));
        Assert.That(persisted.IsDeleted, Is.False);
        Assert.That(persisted.VisibleToTrainee, Is.True);
        Assert.That(persisted.IsPinned, Is.False);
        Assert.That((await verificationDatabase.TraineeNoteHistories.CountAsync(candidate => candidate.TraineeNoteId == note.Id)), Is.EqualTo(1));
    }

    private async Task SeedRelationshipAsync(
        Id<User> trainerId,
        Id<User> traineeId,
        bool addTrainerRole = true)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (addTrainerRole)
        {
            database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainerId));
        }

        database.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId
        });
        await database.SaveChangesAsync();
    }

    private async Task SeedTrainerRoleAsync(Id<User> trainerId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainerId));
        await database.SaveChangesAsync();
    }

    private static TraineeNote VisibleNote(
        Id<User> trainerId,
        Id<User> traineeId,
        string content,
        DateTimeOffset lastUpdatedAt,
        bool visible = true,
        bool isPinned = false,
        bool isDeleted = false)
        => new()
        {
            Id = Id<TraineeNote>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            Content = content,
            VisibleToTrainee = visible,
            IsPinned = isPinned,
            LastUpdatedByUserId = trainerId,
            LastUpdatedAt = lastUpdatedAt,
            IsDeleted = isDeleted
        };

    private static TraineeNoteHistory History(TraineeNote note, Id<User> trainerId, string previousContent, string newContent, bool previousVisible, bool newVisible, string changeType, DateTimeOffset changedAt)
        => new()
        {
            Id = Id<TraineeNoteHistory>.New(),
            TraineeNoteId = note.Id,
            ChangedByUserId = trainerId,
            ChangedAt = changedAt,
            PreviousContent = previousContent,
            NewContent = newContent,
            PreviousVisibleToTrainee = previousVisible,
            NewVisibleToTrainee = newVisible,
            ChangeType = changeType
        };
}
