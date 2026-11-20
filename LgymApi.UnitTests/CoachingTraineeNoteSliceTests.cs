using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Coaching.TraineeNotes.Create;
using LgymApi.Application.Coaching.TraineeNotes.Delete;
using LgymApi.Application.Coaching.TraineeNotes.History;
using LgymApi.Application.Coaching.TraineeNotes.Models;
using LgymApi.Application.Coaching.TraineeNotes.TrainerList;
using LgymApi.Application.Coaching.TraineeNotes.Update;
using LgymApi.Application.Coaching.TraineeNotes.VisibleList;
using LgymApi.Application.Coaching.TraineeNotes.VisibleSingle;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Coaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using LgymApi.Resources;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CoachingTraineeNoteSliceTests
{
    [Test]
    public async Task TrainerList_ReturnsMappedOwnedNotesWithoutCommit()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.GrantAccess(trainerId, traineeId);
        var note = Note(trainerId, traineeId, content: "Current", isPinned: true);
        dependencies.Notes.GetNotesByTrainerAndTraineeAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns([note]);

        var result = await Resolve<IListTrainerNotesUseCase>(dependencies.CreateServices())
            .ExecuteAsync(new ListTrainerNotesQuery(trainerId, traineeId));

        Assert.That(result.IsSuccess, Is.True);
        var assertedValue94 = (result.Value).ToArray();
        Assert.That(assertedValue94, Has.Exactly(1).Items);
        Assert.That(assertedValue94.Single(), Is.EqualTo(new TraineeNoteReadModel(
            note.Id,
            note.TrainerId,
            note.TraineeId,
            note.Title,
            note.Content,
            note.VisibleToTrainee,
            note.IsPinned,
            note.LastUpdatedByUserId,
            note.LastUpdatedAt,
            note.CreatedAt,
            note.UpdatedAt,
            "Original trainer")));
        await dependencies.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task VisibleList_PreservesPersistenceOrderAndMapsWithoutAccessOrWrites()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var first = Note(trainerId, traineeId, content: "Pinned", visibleToTrainee: true, isPinned: true);
        var second = Note(trainerId, traineeId, content: "Recent", visibleToTrainee: true);
        var dependencies = new Dependencies();
        using var cancellation = new CancellationTokenSource();
        dependencies.Notes.GetVisibleNotesByTraineeAsync(traineeId, cancellation.Token)
            .Returns([first, second]);

        var result = await Resolve<IListVisibleTraineeNotesUseCase>(dependencies.CreateServices())
            .ExecuteAsync(new ListVisibleTraineeNotesQuery(traineeId), cancellation.Token);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Select(note => note.Id), Is.EqualTo(new[] { first.Id, second.Id }));
        Assert.That(result.Value.Select(note => note.Content), Is.EqualTo(new[] { "Pinned", "Recent" }));
        await dependencies.Notes.Received(1).GetVisibleNotesByTraineeAsync(traineeId, cancellation.Token);
        await dependencies.Access.DidNotReceive().GetAccessDecisionAsync(
            Arg.Any<Id<User>>(),
            Arg.Any<Id<User>>(),
            Arg.Any<CancellationToken>());
        await dependencies.Notes.DidNotReceive().AddNoteAsync(
            Arg.Any<CoachingTraineeNoteWriteModel>(),
            Arg.Any<CancellationToken>());
        await dependencies.Notes.DidNotReceive().UpdateNoteAsync(
            Arg.Any<CoachingTraineeNoteWriteModel>(),
            Arg.Any<CancellationToken>());
        await dependencies.Notes.DidNotReceive().AddHistoryEntryAsync(
            Arg.Any<CoachingTraineeNoteHistoryWriteModel>(),
            Arg.Any<CancellationToken>());
        await dependencies.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task VisibleSingle_MapsOwnedVisibleNoteAndRejectsInvalidOrUnavailableNotesWithoutWrites()
    {
        var traineeId = Id<User>.New();
        var otherTraineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var visible = Note(trainerId, traineeId, content: "Visible", visibleToTrainee: true);
        var privateNote = Note(trainerId, traineeId, content: "Private");
        var foreignNote = Note(trainerId, otherTraineeId, content: "Foreign", visibleToTrainee: true);
        var missingNoteId = Id<TraineeNote>.New();
        var dependencies = new Dependencies();
        dependencies.Notes.FindNoteByIdAsync(visible.Id, Arg.Any<CancellationToken>()).Returns(visible);
        dependencies.Notes.FindNoteByIdAsync(privateNote.Id, Arg.Any<CancellationToken>()).Returns(privateNote);
        dependencies.Notes.FindNoteByIdAsync(foreignNote.Id, Arg.Any<CancellationToken>()).Returns(foreignNote);
        dependencies.Notes.FindNoteByIdAsync(missingNoteId, Arg.Any<CancellationToken>())
            .Returns((CoachingTraineeNoteFact?)null);
        var useCase = Resolve<IGetVisibleTraineeNoteUseCase>(dependencies.CreateServices());

        var success = await useCase.ExecuteAsync(new GetVisibleTraineeNoteQuery(traineeId, visible.Id));
        var invalid = await useCase.ExecuteAsync(new GetVisibleTraineeNoteQuery(traineeId, Id<TraineeNote>.Empty));
        var missing = await useCase.ExecuteAsync(new GetVisibleTraineeNoteQuery(traineeId, missingNoteId));
        var privateResult = await useCase.ExecuteAsync(new GetVisibleTraineeNoteQuery(traineeId, privateNote.Id));
        var foreign = await useCase.ExecuteAsync(new GetVisibleTraineeNoteQuery(traineeId, foreignNote.Id));

        Assert.That(success.IsSuccess, Is.True);
        Assert.That(success.Value.Id, Is.EqualTo(visible.Id));
        Assert.That(success.Value.Content, Is.EqualTo("Visible"));
        Assert.That(invalid.Error, Is.TypeOf<BadRequestError>());
        Assert.That(invalid.Error.Message, Is.EqualTo(Messages.FieldRequired));
        Assert.That(missing.Error, Is.TypeOf<NotFoundError>());
        Assert.That(missing.Error.Message, Is.EqualTo(Messages.DidntFind));
        Assert.That(privateResult.Error, Is.TypeOf<NotFoundError>());
        Assert.That(privateResult.Error.Message, Is.EqualTo(Messages.DidntFind));
        Assert.That(foreign.Error, Is.TypeOf<NotFoundError>());
        Assert.That(foreign.Error.Message, Is.EqualTo(Messages.DidntFind));
        await dependencies.Notes.DidNotReceive().FindNoteByIdAsync(
            Id<TraineeNote>.Empty,
            Arg.Any<CancellationToken>());
        await dependencies.Access.DidNotReceive().GetAccessDecisionAsync(
            Arg.Any<Id<User>>(),
            Arg.Any<Id<User>>(),
            Arg.Any<CancellationToken>());
        await dependencies.Notes.DidNotReceive().UpdateNoteAsync(
            Arg.Any<CoachingTraineeNoteWriteModel>(),
            Arg.Any<CancellationToken>());
        await dependencies.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Create_NormalizesStagesHistoryCommitsThenNotifiesWithNullableTitle()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var operations = new List<string>();
        var dependencies = new Dependencies();
        CoachingTraineeNoteWriteModel? stagedNote = null;
        CoachingTraineeNoteHistoryWriteModel? stagedHistory = null;
        TraineeNoteUpdatedInAppNotificationCommand? notification = null;
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, true))
            .AndDoes(_ => operations.Add("access"));
        dependencies.Notes.AddNoteAsync(Arg.Any<CoachingTraineeNoteWriteModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                stagedNote = call.Arg<CoachingTraineeNoteWriteModel>();
                operations.Add("note");
            });
        dependencies.Notes.AddHistoryEntryAsync(Arg.Any<CoachingTraineeNoteHistoryWriteModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                stagedHistory = call.Arg<CoachingTraineeNoteHistoryWriteModel>();
                operations.Add("history");
            });
        dependencies.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(2))
            .AndDoes(_ => operations.Add("commit"));
        dependencies.Commands.EnqueueAsync(Arg.Any<TraineeNoteUpdatedInAppNotificationCommand>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                notification = call.Arg<TraineeNoteUpdatedInAppNotificationCommand>();
                operations.Add("notify");
            });
        dependencies.Notes.FindNoteByIdAsync(Arg.Any<Id<TraineeNote>>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToFact(stagedNote!))
            .AndDoes(_ => operations.Add("read"));

        var result = await Resolve<ICreateTraineeNoteUseCase>(dependencies.CreateServices()).ExecuteAsync(
            new CreateTraineeNoteCommand(
                trainerId,
                traineeId,
                new TraineeNoteUpsertData("   ", "  First note  ", true, true)));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(stagedNote, Is.Not.Null);
        Assert.That(stagedNote!.Title, Is.Null);
        Assert.That(stagedNote.Content, Is.EqualTo("First note"));
        Assert.That(stagedNote.VisibleToTrainee, Is.True);
        Assert.That(stagedNote.IsPinned, Is.True);
        Assert.That(stagedNote.IsDeleted, Is.False);
        Assert.That(stagedHistory, Is.Not.Null);
        Assert.That(stagedHistory!.TraineeNoteId, Is.EqualTo(stagedNote.Id));
        Assert.That(stagedHistory.PreviousContent, Is.Null);
        Assert.That(stagedHistory.NewContent, Is.EqualTo("First note"));
        Assert.That(stagedHistory.ChangeType, Is.EqualTo("Created"));
        Assert.That(stagedHistory.ChangedAt, Is.GreaterThanOrEqualTo(stagedNote.LastUpdatedAt));
        Assert.That(notification, Is.Not.Null);
        Assert.That(notification!.TraineeNoteId, Is.EqualTo(stagedNote.Id));
        Assert.That(notification.TraineeId, Is.EqualTo(traineeId));
        Assert.That(notification.TrainerId, Is.EqualTo(trainerId));
        Assert.That(notification.NoteTitle, Is.Null);
        Assert.That(operations, Is.EqualTo(new[] { "access", "note", "history", "commit", "notify", "read" }));
        await dependencies.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Create_WithEmptyContentChecksRelationshipBeforeReturningBadRequest()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var dependencies = new Dependencies();
        dependencies.GrantAccess(trainerId, traineeId);

        var result = await Resolve<ICreateTraineeNoteUseCase>(dependencies.CreateServices()).ExecuteAsync(
            new CreateTraineeNoteCommand(
                trainerId,
                traineeId,
                new TraineeNoteUpsertData(null, "  ", false, false)));

        Assert.That(result.Error, Is.TypeOf<BadRequestError>());
        await dependencies.Access.Received(1).GetAccessDecisionAsync(
            trainerId,
            traineeId,
            Arg.Any<CancellationToken>());
        await dependencies.Notes.DidNotReceive().AddNoteAsync(
            Arg.Any<CoachingTraineeNoteWriteModel>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Update_UnsharesOwnedNoteWithoutNotification()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var existing = Note(trainerId, traineeId, title: "Old", content: "Old content", visibleToTrainee: true);
        var operations = new List<string>();
        var dependencies = new Dependencies();
        CoachingTraineeNoteWriteModel? stagedNote = null;
        CoachingTraineeNoteHistoryWriteModel? stagedHistory = null;
        dependencies.GrantAccess(trainerId, traineeId);
        dependencies.Notes.FindNoteByIdAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(_ => stagedNote is null ? existing : ToFact(stagedNote));
        dependencies.Notes.UpdateNoteAsync(Arg.Any<CoachingTraineeNoteWriteModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                stagedNote = call.Arg<CoachingTraineeNoteWriteModel>();
                operations.Add("note");
            });
        dependencies.Notes.AddHistoryEntryAsync(Arg.Any<CoachingTraineeNoteHistoryWriteModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                stagedHistory = call.Arg<CoachingTraineeNoteHistoryWriteModel>();
                operations.Add("history");
            });
        dependencies.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(2))
            .AndDoes(_ => operations.Add("commit"));
        dependencies.Commands.EnqueueAsync(Arg.Any<TraineeNoteUpdatedInAppNotificationCommand>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => operations.Add("notify"));

        var result = await Resolve<IUpdateTraineeNoteUseCase>(dependencies.CreateServices()).ExecuteAsync(
            new UpdateTraineeNoteCommand(
                trainerId,
                traineeId,
                existing.Id,
                new TraineeNoteUpsertData("  New title  ", "  New content  ", false, true)));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Title, Is.EqualTo("New title"));
        Assert.That(result.Value.Content, Is.EqualTo("New content"));
        Assert.That(result.Value.VisibleToTrainee, Is.False);
        Assert.That(stagedHistory, Is.Not.Null);
        Assert.That(stagedHistory!.PreviousContent, Is.EqualTo("Old content"));
        Assert.That(stagedHistory.NewContent, Is.EqualTo("New content"));
        Assert.That(stagedHistory.ChangeType, Is.EqualTo("Updated"));
        Assert.That(operations, Is.EqualTo(new[] { "note", "history", "commit" }));
        await dependencies.Commands.DidNotReceive().EnqueueAsync(
            Arg.Any<TraineeNoteUpdatedInAppNotificationCommand>());
        await dependencies.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Update_InvisibleNoteRemainingInvisibleDoesNotNotify()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var existing = Note(trainerId, traineeId, visibleToTrainee: false);
        var dependencies = new Dependencies();
        CoachingTraineeNoteWriteModel? stagedNote = null;
        dependencies.GrantAccess(trainerId, traineeId);
        dependencies.Notes.FindNoteByIdAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(_ => stagedNote is null ? existing : ToFact(stagedNote));
        dependencies.Notes.UpdateNoteAsync(Arg.Any<CoachingTraineeNoteWriteModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => stagedNote = call.Arg<CoachingTraineeNoteWriteModel>());

        var result = await Resolve<IUpdateTraineeNoteUseCase>(dependencies.CreateServices()).ExecuteAsync(
            new UpdateTraineeNoteCommand(
                trainerId,
                traineeId,
                existing.Id,
                new TraineeNoteUpsertData(null, "Still private", false, false)));

        Assert.That(result.IsSuccess, Is.True);
        await dependencies.Commands.DidNotReceive().EnqueueAsync(Arg.Any<IActionCommand>());
    }

    [Test]
    public async Task Update_WithEmptyContentRejectsBeforeRelationshipAccess()
    {
        var dependencies = new Dependencies();

        var result = await Resolve<IUpdateTraineeNoteUseCase>(dependencies.CreateServices()).ExecuteAsync(
            new UpdateTraineeNoteCommand(
                Id<User>.New(),
                Id<User>.New(),
                Id<TraineeNote>.New(),
                new TraineeNoteUpsertData(null, string.Empty, false, false)));

        Assert.That(result.Error, Is.TypeOf<BadRequestError>());
        await dependencies.Access.DidNotReceive().GetAccessDecisionAsync(
            Arg.Any<Id<User>>(),
            Arg.Any<Id<User>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Delete_SoftDeletesAndStagesDeletedHistoryAtOneCommitWithoutNotification()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var existing = Note(trainerId, traineeId, content: "Keep in history", visibleToTrainee: true, isPinned: true);
        var dependencies = new Dependencies();
        CoachingTraineeNoteWriteModel? deleted = null;
        CoachingTraineeNoteHistoryWriteModel? history = null;
        dependencies.GrantAccess(trainerId, traineeId);
        dependencies.Notes.FindNoteByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        dependencies.Notes.UpdateNoteAsync(Arg.Any<CoachingTraineeNoteWriteModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => deleted = call.Arg<CoachingTraineeNoteWriteModel>());
        dependencies.Notes.AddHistoryEntryAsync(Arg.Any<CoachingTraineeNoteHistoryWriteModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => history = call.Arg<CoachingTraineeNoteHistoryWriteModel>());

        var result = await Resolve<IDeleteTraineeNoteUseCase>(dependencies.CreateServices())
            .ExecuteAsync(new DeleteTraineeNoteCommand(trainerId, traineeId, existing.Id));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(deleted, Is.Not.Null);
        Assert.That(deleted!.IsDeleted, Is.True);
        Assert.That(deleted.VisibleToTrainee, Is.False);
        Assert.That(deleted.IsPinned, Is.True);
        Assert.That(deleted.Content, Is.EqualTo("Keep in history"));
        Assert.That(history, Is.Not.Null);
        Assert.That(history!.PreviousContent, Is.EqualTo("Keep in history"));
        Assert.That(history.NewContent, Is.EqualTo("Keep in history"));
        Assert.That(history.ChangeType, Is.EqualTo("Deleted"));
        await dependencies.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await dependencies.Commands.DidNotReceive().EnqueueAsync(Arg.Any<IActionCommand>());
    }

    [Test]
    public async Task History_ReturnsMappedRowsForOwnedNoteWithoutCommit()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var note = Note(trainerId, traineeId);
        var changedAt = DateTimeOffset.UtcNow;
        var history = new CoachingTraineeNoteHistoryFact(
            Id<TraineeNoteHistory>.New(),
            note.Id,
            trainerId,
            changedAt,
            "Before",
            "After",
            true,
            true,
            "Updated",
            changedAt,
            changedAt);
        var dependencies = new Dependencies();
        dependencies.GrantAccess(trainerId, traineeId);
        dependencies.Notes.FindNoteByIdAsync(note.Id, Arg.Any<CancellationToken>()).Returns(note);
        dependencies.Notes.GetNoteHistoryAsync(note.Id, Arg.Any<CancellationToken>()).Returns([history]);

        var result = await Resolve<IGetTraineeNoteHistoryUseCase>(dependencies.CreateServices())
            .ExecuteAsync(new GetTraineeNoteHistoryQuery(trainerId, traineeId, note.Id));

        Assert.That(result.IsSuccess, Is.True);
        var assertedValue95 = (result.Value).ToArray();
        Assert.That(assertedValue95, Has.Exactly(1).Items);
        Assert.That(assertedValue95.Single(), Is.EqualTo(new TraineeNoteHistoryReadModel(
            history.Id,
            history.TraineeNoteId,
            history.ChangedByUserId,
            history.ChangedAt,
            history.PreviousContent,
            history.NewContent,
            history.ChangeType)));
        await dependencies.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TrainerSlices_AllowFormerAuthorListAndRejectForeignNoteAndEmptyNoteIdWithoutWrites()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var foreignTrainerId = Id<User>.New();
        var noteId = Id<TraineeNote>.New();
        var dependencies = new Dependencies();
        dependencies.Access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, false));
        var services = dependencies.CreateServices();

        var foreignRelationship = await Resolve<IListTrainerNotesUseCase>(services)
            .ExecuteAsync(new ListTrainerNotesQuery(trainerId, traineeId));

        dependencies.GrantAccess(trainerId, traineeId);
        dependencies.Notes.FindNoteByIdAsync(noteId, Arg.Any<CancellationToken>())
            .Returns(Note(foreignTrainerId, traineeId, noteId: noteId));
        var foreignNote = await Resolve<IUpdateTraineeNoteUseCase>(services).ExecuteAsync(
            new UpdateTraineeNoteCommand(
                trainerId,
                traineeId,
                noteId,
                new TraineeNoteUpsertData(null, "Content", false, false)));
        var invalidId = await Resolve<IDeleteTraineeNoteUseCase>(services)
            .ExecuteAsync(new DeleteTraineeNoteCommand(trainerId, traineeId, Id<TraineeNote>.Empty));

        Assert.That(foreignRelationship.IsSuccess, Is.True);
        Assert.That(foreignNote.Error, Is.TypeOf<NotFoundError>());
        Assert.That(invalidId.Error, Is.TypeOf<BadRequestError>());
        await dependencies.Notes.DidNotReceive().UpdateNoteAsync(
            Arg.Any<CoachingTraineeNoteWriteModel>(),
            Arg.Any<CancellationToken>());
        await dependencies.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Create_WhenOwnerCommitFailsPersistsNeitherNoteNorHistoryAndDoesNotNotify()
    {
        var databaseName = $"coaching-note-atomic-{Id<CoachingTraineeNoteSliceTests>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal)}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await using var database = new AppDbContext(options);
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var access = Substitute.For<ICoachingRelationshipAccessService>();
        var episodeAuthorization = Substitute.For<ICoachingEpisodeAuthorizationService>();
        var accounts = Substitute.For<IAccountReadService>();
        var commands = Substitute.For<ICommandDispatcher>();
        access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
            .Returns(new CoachingRelationshipAccessDecision(true, true));
        episodeAuthorization.AuthorizeAndStageAsync(
                trainerId,
                trainerId,
                traineeId,
                CoachingEpisodeAuthorizationAction.ProtectedWrite,
                Arg.Any<CancellationToken>())
            .Returns(new CoachingEpisodeAuthorizationGrant(
                Id<TrainerTraineeLink>.New(),
                Id<CoachingEpisode>.New(),
                trainerId,
                traineeId,
                0));
        accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>())
            .Returns(new AccountReadModel(trainerId, "Original trainer", "trainer@example.test", null, "en", "UTC"));
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        services.AddCoachingModule();
        services.AddScoped(_ => database);
        services.AddScoped<ICoachingTraineeNotePersistence, CoachingTraineeNotePersistenceRepository>();
        services.AddScoped(_ => access);
        services.AddScoped(_ => episodeAuthorization);
        services.AddScoped(_ => accounts);
        services.AddScoped(_ => commands);
        services.AddScoped<IUnitOfWork>(_ => new ThrowingUnitOfWork());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Func<Task> act = () => scope.ServiceProvider.GetRequiredService<ICreateTraineeNoteUseCase>().ExecuteAsync(
            new CreateTraineeNoteCommand(
                trainerId,
                traineeId,
                new TraineeNoteUpsertData("Atomic", "Both or neither", true, false)));

        await Assert.ThatAsync(async () => { await act(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("commit failed").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That((database.ChangeTracker.Entries<TraineeNote>()).Where(entry => entry.State == EntityState.Added), Has.Exactly(1).Items);
        Assert.That((database.ChangeTracker.Entries<TraineeNoteHistory>()).Where(entry => entry.State == EntityState.Added), Has.Exactly(1).Items);
        await commands.DidNotReceive().EnqueueAsync(Arg.Any<IActionCommand>());

        await using var verificationDatabase = new AppDbContext(options);
        Assert.That((await verificationDatabase.TraineeNotes.IgnoreQueryFilters().CountAsync()), Is.EqualTo(0));
        Assert.That((await verificationDatabase.TraineeNoteHistories.IgnoreQueryFilters().CountAsync()), Is.EqualTo(0));
    }

    private static TContract Resolve<TContract>(ServiceCollection services) where TContract : notnull
    {
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TContract>();
    }

    private static CoachingTraineeNoteFact Note(
        Id<User> trainerId,
        Id<User> traineeId,
        Id<TraineeNote>? noteId = null,
        string? title = "Title",
        string content = "Content",
        bool visibleToTrainee = false,
        bool isPinned = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new CoachingTraineeNoteFact(
            noteId ?? Id<TraineeNote>.New(),
            trainerId,
            traineeId,
            title,
            content,
            visibleToTrainee,
            isPinned,
            trainerId,
            now,
            now,
            now);
    }

    private static CoachingTraineeNoteFact ToFact(CoachingTraineeNoteWriteModel note)
        => new(
            note.Id,
            note.TrainerId,
            note.TraineeId,
            note.Title,
            note.Content,
            note.VisibleToTrainee,
            note.IsPinned,
            note.LastUpdatedByUserId,
            note.LastUpdatedAt,
            note.LastUpdatedAt,
            note.LastUpdatedAt);

    private sealed class Dependencies
    {
        public ICoachingRelationshipAccessService Access { get; } = Substitute.For<ICoachingRelationshipAccessService>();
        public ICoachingEpisodeAuthorizationService EpisodeAuthorization { get; } = Substitute.For<ICoachingEpisodeAuthorizationService>();
        public ICoachingTraineeNotePersistence Notes { get; } = Substitute.For<ICoachingTraineeNotePersistence>();
        public IAccountReadService Accounts { get; } = Substitute.For<IAccountReadService>();
        public ICommandDispatcher Commands { get; } = Substitute.For<ICommandDispatcher>();
        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public Dependencies()
        {
            EpisodeAuthorization.AuthorizeAndStageAsync(
                    Arg.Any<Id<User>>(),
                    Arg.Any<Id<User>>(),
                    Arg.Any<Id<User>>(),
                    CoachingEpisodeAuthorizationAction.ProtectedWrite,
                    Arg.Any<CancellationToken>())
                .Returns(call => new CoachingEpisodeAuthorizationGrant(
                    Id<TrainerTraineeLink>.New(),
                    Id<CoachingEpisode>.New(),
                    call.ArgAt<Id<User>>(1),
                    call.ArgAt<Id<User>>(2),
                    0));
            Accounts.GetByIdAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>())
                .Returns(call => new AccountReadModel(
                    call.Arg<Id<User>>(),
                    "Original trainer",
                    "trainer@example.test",
                    null,
                    "en",
                    "UTC"));
            Accounts.GetByIdsAsync(Arg.Any<IReadOnlyList<Id<User>>>(), Arg.Any<CancellationToken>())
                .Returns(call => (IReadOnlyList<AccountReadModel>)call.Arg<IReadOnlyList<Id<User>>>()
                    .Select(id => new AccountReadModel(id, "Original trainer", "trainer@example.test", null, "en", "UTC"))
                    .ToList());
        }

        public void GrantAccess(Id<User> trainerId, Id<User> traineeId)
            => Access.GetAccessDecisionAsync(trainerId, traineeId, Arg.Any<CancellationToken>())
                .Returns(new CoachingRelationshipAccessDecision(true, true));

        public ServiceCollection CreateServices()
        {
            var services = new ServiceCollection();
            services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
            services.AddCoachingModule();
            services.AddScoped(_ => Access);
            services.AddScoped(_ => EpisodeAuthorization);
            services.AddScoped(_ => Notes);
            services.AddScoped(_ => Accounts);
            services.AddScoped(_ => Commands);
            services.AddScoped(_ => UnitOfWork);
            return services;
        }
    }

    private sealed class ThrowingUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromException<int>(new InvalidOperationException("commit failed"));

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
