using FluentAssertions;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using Microsoft.Extensions.Logging.Abstractions;
using LgymApi.Application.Features.Exercise;
using LgymApi.Application.Features.Exercise.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Application.WorkoutProgress.Authorization;
using LgymApi.Application.WorkoutProgress.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.TrainingPlanning.Contracts;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseServiceTests
{
    private IAccountAccessReader _accounts = null!;
    private IWorkoutExercisePersistence _exercises = null!;
    private IWorkoutExerciseScorePersistence _scores = null!;
    private IPlanDayReferenceReadService _planDays = null!;
    private IUnitOfWork _unitOfWork = null!;
    private IAuthenticatedAccountAuthorizationService _accountAuthorization = null!;
    private IWorkoutProgressOwnershipAuthorizationService _ownershipAuthorization = null!;
    private IExerciseMediaStorage _mediaStorage = null!;
    private ExerciseService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _accounts = Substitute.For<IAccountAccessReader>();
        _exercises = Substitute.For<IWorkoutExercisePersistence>();
        _scores = Substitute.For<IWorkoutExerciseScorePersistence>();
        _planDays = Substitute.For<IPlanDayReferenceReadService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _accountAuthorization = Substitute.For<IAuthenticatedAccountAuthorizationService>();
        _ownershipAuthorization = Substitute.For<IWorkoutProgressOwnershipAuthorizationService>();
        _accountAuthorization.HasPermission(Arg.Any<AuthenticatedAccountContext>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<AuthenticatedAccountContext>(0).PermissionClaims.Contains(call.ArgAt<string>(1), StringComparer.Ordinal));
        _ownershipAuthorization.IsOwner(Arg.Any<AuthenticatedAccountContext?>(), Arg.Any<Id<AccountReference>>())
            .Returns(call =>
            {
                var actor = call.ArgAt<AuthenticatedAccountContext?>(0);
                return actor is not null && actor.Id == call.ArgAt<Id<AccountReference>>(1);
            });
        _mediaStorage = Substitute.For<IExerciseMediaStorage>();
        _service = new ExerciseService(_accounts, _exercises, _scores, _planDays, _unitOfWork, _accountAuthorization, _ownershipAuthorization, _mediaStorage, NullLogger<ExerciseService>.Instance);
    }

    [Test]
    public async Task AddGlobalExercise_WithoutPermission_ReturnsForbiddenWithoutWriting()
    {
        var actor = Context(Id<AccountReference>.New());

        var result = await _service.AddExerciseAsync(actor, "Squat", BodyParts.Quads, null, null);

        result.Error.Should().BeOfType<ExerciseForbiddenError>();
        await _exercises.DidNotReceive().AddAsync(Arg.Any<WorkoutExerciseWriteModel>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddGlobalExercise_AsManager_StagesGlobalExerciseAndCommits()
    {
        var manager = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);

        var result = await _service.AddExerciseAsync(manager, "Squat", BodyParts.Quads, null, null);

        result.IsSuccess.Should().BeTrue();
        await _exercises.Received(1).AddAsync(
            Arg.Is<WorkoutExerciseWriteModel>(exercise => exercise.OwnerId == null),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddUserExercise_WhenRouteDoesNotMatchActor_ReturnsForbiddenWithoutWriting()
    {
        var actor = Context(Id<AccountReference>.New());
        var victimId = Id<AccountReference>.New();

        var result = await _service.AddUserExerciseAsync(
            actor,
            new AddUserExerciseInput(victimId, "Squat", BodyParts.Quads, null, null));

        result.Error.Should().BeOfType<ExerciseForbiddenError>();
        await _exercises.DidNotReceive().AddAsync(Arg.Any<WorkoutExerciseWriteModel>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddUserExercise_AsOwner_StagesMarkerOwnedExerciseAndCommits()
    {
        var accountId = Id<AccountReference>.New();
        var actor = Context(accountId);

        var result = await _service.AddUserExerciseAsync(
            actor,
            new AddUserExerciseInput(accountId, "Squat", BodyParts.Quads, null, null));

        result.IsSuccess.Should().BeTrue();
        await _exercises.Received(1).AddAsync(
            Arg.Is<WorkoutExerciseWriteModel>(exercise => exercise.OwnerId == accountId),
            Arg.Any<CancellationToken>());
        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<Id<AccountReference>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddUserExerciseWithFormula_AsManager_CannotTargetForeignAccount()
    {
        var manager = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        var targetId = Id<AccountReference>.New();
        var result = await _service.AddUserExerciseWithFormulaAsync(
            manager,
            new AddUserExerciseWithFormulaInput(targetId, "Squat", BodyParts.Quads, ExerciseEloFormula.VolumeWeighted, null, null));

        result.Error.Should().BeOfType<ExerciseForbiddenError>();
        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<Id<AccountReference>>(), Arg.Any<CancellationToken>());
        await _exercises.DidNotReceive().AddAsync(Arg.Any<WorkoutExerciseWriteModel>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddUserExerciseWithFormula_AsManagerForOwnAccount_StagesOwnedExercise()
    {
        var manager = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        _accounts.GetByIdAsync(manager.Id, Arg.Any<CancellationToken>()).Returns(Facts(manager.Id));

        var result = await _service.AddUserExerciseWithFormulaAsync(
            manager,
            new AddUserExerciseWithFormulaInput(manager.Id, "Squat", BodyParts.Quads, ExerciseEloFormula.VolumeWeighted, null, null));

        result.IsSuccess.Should().BeTrue();
        await _exercises.Received(1).AddAsync(
            Arg.Is<WorkoutExerciseWriteModel>(exercise => exercise.OwnerId == manager.Id && exercise.EloFormula == ExerciseEloFormula.VolumeWeighted),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteExercise_WhenRouteDoesNotMatchActor_ReturnsForbiddenWithoutLookupOrSave()
    {
        var actor = Context(Id<AccountReference>.New());

        var result = await _service.DeleteExerciseAsync(actor, Id<AccountReference>.New(), Id<Exercise>.New());

        result.Error.Should().BeOfType<ExerciseForbiddenError>();
        await _exercises.DidNotReceive().FindOwnedByAccountAsync(Arg.Any<Id<Exercise>>(), Arg.Any<Id<AccountReference>>(), Arg.Any<CancellationToken>());
        await _exercises.DidNotReceive().FindUnrestrictedByIdAsync(Arg.Any<Id<Exercise>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteExercise_AsOwner_UsesOwnedLookupAndCommits()
    {
        var accountId = Id<AccountReference>.New();
        var exerciseId = Id<Exercise>.New();
        var actor = Context(accountId);
        _exercises.FindOwnedByAccountAsync(exerciseId, accountId, Arg.Any<CancellationToken>()).Returns(Model(exerciseId, accountId));

        var result = await _service.DeleteExerciseAsync(actor, accountId, exerciseId);

        result.IsSuccess.Should().BeTrue();
        await _exercises.Received(1).UpdateAsync(
            Arg.Is<WorkoutExerciseWriteModel>(exercise => exercise.Id == exerciseId && exercise.IsDeleted),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteExercise_AsManager_DeletesGlobalExerciseAndCommits()
    {
        var managerId = Id<AccountReference>.New();
        var exerciseId = Id<Exercise>.New();
        var manager = Context(managerId, [AuthConstants.Permissions.ManageGlobalExercises]);
        _exercises.FindGlobalByIdAsync(exerciseId, Arg.Any<CancellationToken>()).Returns(Model(exerciseId, null));

        var result = await _service.DeleteExerciseAsync(manager, managerId, exerciseId);

        result.IsSuccess.Should().BeTrue();
        await _exercises.Received(1).UpdateAsync(
            Arg.Is<WorkoutExerciseWriteModel>(exercise => exercise.Id == exerciseId && exercise.OwnerId == null && exercise.IsDeleted),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteExercise_AsManager_DeletesOwnCustomExercise()
    {
        var manager = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        var exerciseId = Id<Exercise>.New();
        _exercises.FindOwnedByAccountAsync(exerciseId, manager.Id, Arg.Any<CancellationToken>()).Returns(Model(exerciseId, manager.Id));

        var result = await _service.DeleteExerciseAsync(manager, manager.Id, exerciseId);

        result.IsSuccess.Should().BeTrue();
        await _exercises.DidNotReceive().FindGlobalByIdAsync(exerciseId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAllUserExercises_WhenRouteDoesNotMatchActor_ReturnsForbiddenWithoutReading()
    {
        var actor = Context(Id<AccountReference>.New());

        var result = await _service.GetAllUserExercisesAsync(actor, Id<AccountReference>.New(), []);

        result.Error.Should().BeOfType<ExerciseForbiddenError>();
        await _exercises.DidNotReceive().GetAccountExercisesAsync(Arg.Any<Id<AccountReference>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetExerciseByBodyPart_WhenRouteDoesNotMatchActor_ReturnsForbiddenWithoutReading()
    {
        var actor = Context(Id<AccountReference>.New());

        var result = await _service.GetExerciseByBodyPartAsync(actor, Id<AccountReference>.New(), BodyParts.Chest, []);

        result.Error.Should().BeOfType<ExerciseForbiddenError>();
        await _exercises.DidNotReceive().GetByBodyPartAsync(Arg.Any<Id<AccountReference>>(), Arg.Any<BodyParts>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetExercise_WhenForeignCustomIsNotVisible_ReturnsNotFoundWithoutUnrestrictedLookup()
    {
        var actor = Context(Id<AccountReference>.New());
        var exerciseId = Id<Exercise>.New();
        _exercises.FindVisibleToAccountAsync(exerciseId, actor.Id, Arg.Any<CancellationToken>()).Returns((WorkoutExercisePersistenceModel?)null);

        var result = await _service.GetExerciseAsync(actor, exerciseId, []);

        result.Error.Should().BeOfType<ExerciseNotFoundError>();
        await _exercises.DidNotReceive().FindUnrestrictedByIdAsync(Arg.Any<Id<Exercise>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetExercise_AsManager_CannotReadForeignCustomExercise()
    {
        var manager = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        var exerciseId = Id<Exercise>.New();
        _exercises.FindVisibleToAccountAsync(exerciseId, manager.Id, Arg.Any<CancellationToken>()).Returns((WorkoutExercisePersistenceModel?)null);

        var result = await _service.GetExerciseAsync(manager, exerciseId, []);

        result.Error.Should().BeOfType<ExerciseNotFoundError>();
        await _exercises.Received(1).FindVisibleToAccountAsync(exerciseId, manager.Id, Arg.Any<CancellationToken>());
        await _exercises.DidNotReceive().FindUnrestrictedByIdAsync(Arg.Any<Id<Exercise>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateExercise_WhenForeignCustomIsNotOwned_ReturnsNotFoundWithoutSave()
    {
        var actor = Context(Id<AccountReference>.New());
        var exerciseId = Id<Exercise>.New();
        _exercises.FindOwnedByAccountAsync(exerciseId, actor.Id, Arg.Any<CancellationToken>()).Returns((WorkoutExercisePersistenceModel?)null);

        var result = await _service.UpdateExerciseAsync(
            actor,
            new UpdateExerciseInput(exerciseId, "Changed", BodyParts.Back, null, null));

        result.Error.Should().BeOfType<ExerciseNotFoundError>();
        await _exercises.DidNotReceive().UpdateAsync(Arg.Any<WorkoutExerciseWriteModel>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateExercise_AsManager_UpdatesOwnCustomExercise()
    {
        var manager = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        var exerciseId = Id<Exercise>.New();
        _exercises.FindOwnedByAccountAsync(exerciseId, manager.Id, Arg.Any<CancellationToken>()).Returns(Model(exerciseId, manager.Id));

        var result = await _service.UpdateExerciseAsync(manager, new UpdateExerciseInput(exerciseId, "Changed", BodyParts.Back, null, null));

        result.IsSuccess.Should().BeTrue();
        await _exercises.DidNotReceive().FindGlobalByIdAsync(exerciseId, Arg.Any<CancellationToken>());
        await _exercises.Received(1).UpdateAsync(
            Arg.Is<WorkoutExerciseWriteModel>(exercise => exercise.OwnerId == manager.Id && exercise.Name == "Changed"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddGlobalTranslation_ShouldUsePermissionFactsAndCommit()
    {
        var accountId = Id<AccountReference>.New();
        var exerciseId = Id<Exercise>.New();
        var context = Context(accountId, [AuthConstants.Permissions.ManageGlobalExercises]);
        _exercises.FindGlobalByIdAsync(exerciseId, Arg.Any<CancellationToken>()).Returns(Model(exerciseId, null));

        var result = await _service.AddGlobalTranslationAsync(context, new AddGlobalTranslationInput(accountId, exerciseId, "pl-PL", "Przysiad"));

        result.IsSuccess.Should().BeTrue();
        await _exercises.Received(1).FindGlobalByIdAsync(exerciseId, Arg.Any<CancellationToken>());
        await _exercises.DidNotReceive().FindUnrestrictedByIdAsync(exerciseId, Arg.Any<CancellationToken>());
        await _exercises.Received(1).UpsertTranslationAsync(exerciseId, "pl-pl", "Przysiad", Arg.Any<CancellationToken>());
        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<Id<AccountReference>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetExerciseScoresFromTrainingByExercise_WhenPlanDayIsMissing_KeepsHistoryWithEmptyName()
    {
        var accountId = Id<AccountReference>.New();
        var exerciseId = Id<Exercise>.New();
        var trainingId = Id<Training>.New();
        var gymId = Id<Gym>.New();
        var planDayId = Id<PlanDayReference>.New();
        var gym = new WorkoutGymPersistenceModel(
            gymId,
            accountId,
            "History Gym",
            null,
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var training = new WorkoutTrainingPersistenceModel(
            trainingId,
            accountId,
            planDayId,
            gymId,
            DateTimeOffset.UtcNow,
            gym);
        var score = new WorkoutExerciseScorePersistenceModel(
            Id<ExerciseScore>.New(),
            exerciseId,
            accountId,
            8,
            1,
            100,
            WeightUnits.Kilograms,
            trainingId,
            0,
            DateTimeOffset.UtcNow,
            null,
            training);
        _exercises.FindVisibleToAccountAsync(exerciseId, accountId, Arg.Any<CancellationToken>())
            .Returns(Model(exerciseId, accountId));
        _scores.GetByAccountAndExerciseAsync(accountId, exerciseId, Arg.Any<CancellationToken>())
            .Returns([score]);
        _planDays.GetByIdsAsync(
                Arg.Is<IReadOnlyList<Id<PlanDayReference>>>(ids => ids.Count == 1 && ids[0] == planDayId),
                Arg.Any<CancellationToken>())
            .Returns([
                new PlanDayReferenceReadModel(
                    planDayId,
                    Id<PlanReference>.Empty,
                    string.Empty,
                    Exists: false,
                    IsDeleted: false)
            ]);

        var result = await _service.GetExerciseScoresFromTrainingByExerciseAsync(accountId, exerciseId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].TrainingName.Should().BeEmpty();
        result.Value[0].GymName.Should().Be("History Gym");
        result.Value[0].SeriesScores.Should().ContainSingle();
        result.Value[0].SeriesScores[0].Score!.Weight.Should().Be(100);
    }

    [TestCase(false, false, null)]
    [TestCase(true, false, null)]
    [TestCase(false, true, null)]
    [TestCase(true, true, null)]
    [TestCase(false, false, "https://media.test/new.gif")]
    [TestCase(true, false, "https://media.test/new.gif")]
    [TestCase(false, true, "https://media.test/new.gif")]
    [TestCase(true, true, "https://media.test/new.gif")]
    public async Task ExplicitGlobalImageChange_CommitsBeforeCompleteReferenceCheckAndRetainsWithoutReferenceFence(bool formula, bool referenced, string? nextImage)
    {
        var manager = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        var id = Id<Exercise>.New();
        const string previous = "https://media.test/exercises/previous.gif";
        _exercises.FindGlobalByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Model(id, null) with { Image = previous });
        _mediaStorage.IsConfigured.Returns(true);
        _mediaStorage.OwnsPublicImage(previous).Returns(true);
        var committed = false;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => { committed = true; return 1; });
        _exercises.IsImageReferencedIncludingDeletedAsync(previous, Arg.Any<CancellationToken>()).Returns(_ => { committed.Should().BeTrue(); return referenced; });

        var result = formula
            ? await _service.UpdateExerciseWithFormulaAsync(manager, new(id, "Changed", BodyParts.Chest, ExerciseEloFormula.Standard, null, nextImage, true))
            : await _service.UpdateExerciseAsync(manager, new(id, "Changed", BodyParts.Chest, null, nextImage, true));

        result.IsSuccess.Should().BeTrue();
        await _exercises.Received(1).IsImageReferencedIncludingDeletedAsync(previous, Arg.Any<CancellationToken>());
        await _mediaStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConcurrentReferenceAttach_AfterEmptySnapshot_NeverDeletesReusedImage()
    {
        var actor = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        var id = Id<Exercise>.New();
        const string previous = "https://media.test/exercises/previous.gif";
        _exercises.FindGlobalByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Model(id, null) with { Image = previous });
        _mediaStorage.IsConfigured.Returns(true);
        _mediaStorage.OwnsPublicImage(previous).Returns(true);
        var snapshotTaken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSnapshot = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _exercises.IsImageReferencedIncludingDeletedAsync(previous, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            snapshotTaken.SetResult();
            return releaseSnapshot.Task;
        });

        var save = _service.UpdateExerciseAsync(actor, new(id, "Changed", BodyParts.Chest, null, null, true));
        await snapshotTaken.Task;
        // Another allowed writer attaches the URL after the query took its empty snapshot.
        var competingExercise = Model(Id<Exercise>.New(), null) with { Image = previous };
        await _exercises.UpdateAsync(new(competingExercise.Id, null, competingExercise.Name, competingExercise.BodyPart,
            competingExercise.EloFormula, competingExercise.Description, competingExercise.Image, false, true), CancellationToken.None);
        releaseSnapshot.SetResult(false);

        (await save).IsSuccess.Should().BeTrue();
        await _mediaStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task MetadataIdenticalAndPersonalImageUpdates_NeverAttemptCleanup(bool updateImage, bool personal)
    {
        var actor = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        var id = Id<Exercise>.New();
        const string previous = "https://media.test/exercises/previous.gif";
        if (personal) _exercises.FindOwnedByAccountAsync(id, actor.Id, Arg.Any<CancellationToken>()).Returns(Model(id, actor.Id) with { Image = previous });
        else _exercises.FindGlobalByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Model(id, null) with { Image = previous });
        _mediaStorage.IsConfigured.Returns(true);
        _mediaStorage.OwnsPublicImage(previous).Returns(true);
        var result = await _service.UpdateExerciseAsync(actor, new(id, "Changed", BodyParts.Chest, null, updateImage ? previous : null, updateImage));
        result.IsSuccess.Should().BeTrue();
        await _exercises.DidNotReceive().IsImageReferencedIncludingDeletedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mediaStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CleanupUncertainty_DoesNotTurnConfirmedSaveIntoFailure(bool cancelled)
    {
        var actor = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        var id = Id<Exercise>.New();
        const string previous = "https://media.test/exercises/previous.gif";
        _exercises.FindGlobalByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Model(id, null) with { Image = previous });
        _mediaStorage.IsConfigured.Returns(true);
        _mediaStorage.OwnsPublicImage(previous).Returns(true);
        _exercises.IsImageReferencedIncludingDeletedAsync(previous, Arg.Any<CancellationToken>()).Returns(Task.FromException<bool>(cancelled ? new OperationCanceledException() : new InvalidOperationException("Reference check unavailable")));
        var result = await _service.UpdateExerciseAsync(actor, new(id, "Changed", BodyParts.Chest, null, "https://media.test/new.gif", true));
        result.IsSuccess.Should().BeTrue();
        await _mediaStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FailedDatabaseSave_NeverChecksReferencesOrDeletes()
    {
        var actor = Context(Id<AccountReference>.New(), [AuthConstants.Permissions.ManageGlobalExercises]);
        var id = Id<Exercise>.New();
        _exercises.FindGlobalByIdAsync(id, Arg.Any<CancellationToken>()).Returns(Model(id, null) with { Image = "https://media.test/old.gif" });
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(new InvalidOperationException("Save failed")));
        Func<Task> save = async () => await _service.UpdateExerciseAsync(actor, new(id, "Changed", BodyParts.Chest, null, null, true));
        await save.Should().ThrowAsync<InvalidOperationException>();
        await _exercises.DidNotReceive().IsImageReferencedIncludingDeletedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mediaStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static WorkoutExercisePersistenceModel Model(Id<Exercise> id, Id<AccountReference>? ownerId)
        => new(id, ownerId, "Exercise", BodyParts.Chest, ExerciseEloFormula.Standard, null, null, false, default, default);

    private static AccountAccessFacts Facts(Id<AccountReference> id, IReadOnlyList<string>? permissions = null)
        => new(id, false, false, [], permissions ?? []);

    private static AuthenticatedAccountContext Context(Id<AccountReference> id, IReadOnlyList<string>? permissions = null)
        => new(id, null, [], permissions ?? [], false, false);
}
