using FluentAssertions;
using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Invitations.Accept;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlCoachingEpisodeTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task Relink_AfterClosedEpisodes_CreatesDistinctEpisodeForEachRelationship()
    {
        var (trainerOne, trainerTwo, trainee) = await SeedRelationshipUsersAsync("relink");

        var firstEpisodeId = await AddActiveRelationshipAsync(trainerOne.Id, trainee.Id);
        await UnlinkAsync(trainerOne.Id, trainee.Id, CoachingEpisodeAuthorizationAction.TrainerUnlink);
        var secondEpisodeId = await AddActiveRelationshipAsync(trainerTwo.Id, trainee.Id);
        await UnlinkAsync(trainee.Id, trainee.Id, CoachingEpisodeAuthorizationAction.TraineeDetach);
        var thirdEpisodeId = await AddActiveRelationshipAsync(trainerOne.Id, trainee.Id);

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var episodes = await database.CoachingEpisodes
            .Where(episode => episode.TraineeId == trainee.Id)
            .OrderBy(episode => episode.CreatedAt)
            .ToListAsync();

        episodes.Select(episode => episode.Id).Should().BeEquivalentTo(
            [firstEpisodeId, secondEpisodeId, thirdEpisodeId]);
        episodes.Select(episode => episode.Id).Should().OnlyHaveUniqueItems();
        episodes.Count(episode => episode.EndedAt != null).Should().Be(2);
        episodes.Should().ContainSingle(episode => episode.Id == thirdEpisodeId && episode.EndedAt == null);
    }

    [TestCase(CoachingEpisodeAuthorizationAction.TrainerUnlink)]
    [TestCase(CoachingEpisodeAuthorizationAction.TraineeDetach)]
    public async Task Unlink_ClosesEpisodeAndRemovesActiveLink(CoachingEpisodeAuthorizationAction action)
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync($"unlink-{action}");
        var episodeId = await AddActiveRelationshipAsync(trainer.Id, trainee.Id);
        var actorId = action == CoachingEpisodeAuthorizationAction.TraineeDetach ? trainee.Id : trainer.Id;

        await UnlinkAsync(actorId, trainee.Id, action);

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.TrainerTraineeLinks.AnyAsync(link => link.TraineeId == trainee.Id)).Should().BeFalse();
        var episode = await database.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId);
        episode.EndedAt.Should().NotBeNull();
        episode.AuthorizationRevision.Should().Be(1);
    }

    [Test]
    public async Task DuplicateUnlink_TwoStagedScopes_CommitOrRecoverOneClosureAndRevisionIncrement()
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync("duplicate-unlink");
        var episodeId = await AddActiveRelationshipAsync(trainer.Id, trainee.Id);
        var barrier = new TwoParticipantAsyncBarrier();

        var outcomes = await Task.WhenAll(
            RaceUnlinkAsync(trainer.Id, trainee.Id, barrier),
            RaceUnlinkAsync(trainer.Id, trainee.Id, barrier));

        outcomes.Should().ContainSingle(outcome => outcome.Committed && !outcome.Recovered);
        outcomes.Should().ContainSingle(outcome => !outcome.Committed && outcome.Recovered);

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.TrainerTraineeLinks.AnyAsync(link => link.TraineeId == trainee.Id)).Should().BeFalse();
        var episode = await database.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId);
        episode.EndedAt.Should().NotBeNull();
        episode.AuthorizationRevision.Should().Be(1);
    }

    [Test]
    public async Task StaleProtectedWrite_AfterUnlink_RollsBackBusinessWrite()
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync("stale-write");
        await AddActiveRelationshipAsync(trainer.Id, trainee.Id);
        var probeKey = $"stale-{Id<ApiIdempotencyRecord>.New()}";

        await using var staleScope = Factory.Services.CreateAsyncScope();
        var staleDatabase = staleScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authorization = staleScope.ServiceProvider.GetRequiredService<ICoachingEpisodeAuthorizationService>();
        var grant = await authorization.AuthorizeAndStageAsync(
            trainer.Id,
            trainer.Id,
            trainee.Id,
            CoachingEpisodeAuthorizationAction.ProtectedWrite);
        grant.Should().NotBeNull();
        staleDatabase.ApiIdempotencyRecords.Add(CreateProbe(probeKey));

        await UnlinkAsync(trainer.Id, trainee.Id, CoachingEpisodeAuthorizationAction.TrainerUnlink);

        var staleCommit = () => staleDatabase.SaveChangesAsync();
        await staleCommit.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verificationDatabase.ApiIdempotencyRecords.AnyAsync(record => record.IdempotencyKey == probeKey))
            .Should().BeFalse();
    }

    [Test]
    public async Task ProtectedWrite_CommittedBeforeUnlink_PersistsAndUnlinkSucceeds()
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync("write-before-unlink");
        await AddActiveRelationshipAsync(trainer.Id, trainee.Id);
        var probeKey = $"before-{Id<ApiIdempotencyRecord>.New()}";

        await using (var writeScope = Factory.Services.CreateAsyncScope())
        {
            var database = writeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var authorization = writeScope.ServiceProvider.GetRequiredService<ICoachingEpisodeAuthorizationService>();
            var grant = await authorization.AuthorizeAndStageAsync(
                trainer.Id,
                trainer.Id,
                trainee.Id,
                CoachingEpisodeAuthorizationAction.ProtectedWrite);
            grant.Should().NotBeNull();
            database.ApiIdempotencyRecords.Add(CreateProbe(probeKey));
            await database.SaveChangesAsync();
        }

        await UnlinkAsync(trainer.Id, trainee.Id, CoachingEpisodeAuthorizationAction.TrainerUnlink);

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verificationDatabase.ApiIdempotencyRecords.AnyAsync(record => record.IdempotencyKey == probeKey))
            .Should().BeTrue();
        (await verificationDatabase.TrainerTraineeLinks.AnyAsync(link => link.TraineeId == trainee.Id))
            .Should().BeFalse();
    }

    [Test]
    public async Task UnlinkTransaction_RollbackPreservesLinkAndOpenEpisode()
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync("rollback");
        var episodeId = await AddActiveRelationshipAsync(trainer.Id, trainee.Id);

        await using (var actionScope = Factory.Services.CreateAsyncScope())
        {
            var database = actionScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync();
            var authorization = actionScope.ServiceProvider.GetRequiredService<ICoachingEpisodeAuthorizationService>();
            var activeLinks = actionScope.ServiceProvider.GetRequiredService<ICoachingActiveLinkPersistence>();
            var grant = await authorization.AuthorizeAndStageAsync(
                trainer.Id,
                trainer.Id,
                trainee.Id,
                CoachingEpisodeAuthorizationAction.TrainerUnlink);
            grant.Should().NotBeNull();
            await activeLinks.RemoveAsync(grant!);
            await database.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verificationDatabase.TrainerTraineeLinks.AnyAsync(link => link.TraineeId == trainee.Id))
            .Should().BeTrue();
        var episode = await verificationDatabase.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId);
        episode.EndedAt.Should().BeNull();
        episode.AuthorizationRevision.Should().Be(0);
    }

    [Test]
    public async Task LegacyDelete_ClosesEpisodeAndIncrementsRevision()
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync("legacy-delete");
        var episodeId = await AddActiveRelationshipAsync(trainer.Id, trainee.Id);

        await using (var actionScope = Factory.Services.CreateAsyncScope())
        {
            var database = actionScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"TrainerTraineeLinks\" WHERE \"TraineeId\" = {trainee.Id.ToString()}::uuid");
        }

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var episode = await verificationDatabase.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId);
        episode.EndedAt.Should().NotBeNull();
        episode.AuthorizationRevision.Should().Be(1);
    }

    [Test]
    public async Task LegacyInsert_WithoutEpisodeId_IsRejectedByDatabaseFence()
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync("legacy-insert");
        var linkId = Id<TrainerTraineeLink>.New();

        await using (var actionScope = Factory.Services.CreateAsyncScope())
        {
            var database = actionScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var insert = () => database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "TrainerTraineeLinks" ("Id", "TrainerId", "TraineeId", "CreatedAt", "UpdatedAt", "IsDeleted")
                VALUES ({linkId.ToString()}::uuid, {trainer.Id.ToString()}::uuid, {trainee.Id.ToString()}::uuid, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow}, FALSE)
                """);

            await insert.Should().ThrowAsync<PostgresException>();
        }

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verificationDatabase.TrainerTraineeLinks.IgnoreQueryFilters().AnyAsync(link => link.Id == linkId)).Should().BeFalse();
        (await verificationDatabase.CoachingEpisodes.AnyAsync(episode => episode.TraineeId == trainee.Id)).Should().BeFalse();
    }

    [Test]
    public async Task LegacySoftDelete_ClosesEpisodeAndIncrementsRevisionOnce()
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync("legacy-soft-delete");
        var episodeId = await AddActiveRelationshipAsync(trainer.Id, trainee.Id);

        await using (var actionScope = Factory.Services.CreateAsyncScope())
        {
            var database = actionScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "TrainerTraineeLinks"
                SET "IsDeleted" = TRUE, "UpdatedAt" = {DateTimeOffset.UtcNow}
                WHERE "TraineeId" = {trainee.Id.ToString()}::uuid
                """);
        }

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await verificationDatabase.TrainerTraineeLinks
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.TraineeId == trainee.Id);
        link.IsDeleted.Should().BeTrue();
        var episode = await verificationDatabase.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId);
        episode.EndedAt.Should().NotBeNull();
        episode.AuthorizationRevision.Should().Be(1);
    }

    [Test]
    public async Task LegacyRelationshipRewrite_IsRejectedWithoutChangingEpisodeOwnership()
    {
        var (trainer, otherTrainer, trainee) = await SeedRelationshipUsersAsync("legacy-update");
        var episodeId = await AddActiveRelationshipAsync(trainer.Id, trainee.Id);

        await using (var actionScope = Factory.Services.CreateAsyncScope())
        {
            var database = actionScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rewrite = () => database.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"TrainerTraineeLinks\" SET \"TrainerId\" = {otherTrainer.Id.ToString()}::uuid WHERE \"TraineeId\" = {trainee.Id.ToString()}::uuid");
            await rewrite.Should().ThrowAsync<PostgresException>();
        }

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var episode = await verificationDatabase.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId);
        episode.TrainerId.Should().Be(trainer.Id);
        episode.EndedAt.Should().BeNull();
    }

    [Test]
    public async Task ReportingAccessFact_RetainsClosedEpisodeAsStartInclusiveEndExclusivePeriod()
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync("reporting-period");
        var episodeId = await AddActiveRelationshipAsync(trainer.Id, trainee.Id);
        await UnlinkAsync(trainer.Id, trainee.Id, CoachingEpisodeAuthorizationAction.TrainerUnlink);

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var episode = await database.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId);
        var accessPersistence = scope.ServiceProvider.GetRequiredService<IReportingRelationshipAccessPersistence>();

        var access = await accessPersistence.GetAccessAsync(
            trainer.Id.Rebind<LgymApi.Identity.Contracts.AccountReference>(),
            trainee.Id.Rebind<LgymApi.Identity.Contracts.AccountReference>());

        access.HasActiveRelationship.Should().BeFalse();
        access.RelationshipPeriods.Should().ContainSingle();
        access.HadRelationshipAt(episode.StartedAt!.Value).Should().BeTrue();
        access.HadRelationshipAt(episode.EndedAt!.Value.AddTicks(-1)).Should().BeTrue();
        access.HadRelationshipAt(episode.EndedAt.Value).Should().BeFalse();
    }

    [Test]
    public async Task ReportingAccessFact_ProjectsObservedStartProvenanceWithoutGrantingGenericPastAccess()
    {
        var (trainer, _, trainee) = await SeedRelationshipUsersAsync("reporting-observed-start");
        var episodeId = await AddActiveRelationshipAsync(trainer.Id, trainee.Id);
        await using (var mutationScope = Factory.Services.CreateAsyncScope())
        {
            var mutationDatabase = mutationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var episode = await mutationDatabase.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId);
            episode.StartedAt = null;
            episode.StartedAtProvenance = CoachingEpisodeStartProvenance.ExistingActiveLinkObservedDuringMigration;
            await mutationDatabase.SaveChangesAsync();
        }
        await UnlinkAsync(trainer.Id, trainee.Id, CoachingEpisodeAuthorizationAction.TrainerUnlink);

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var episodeEnd = (await database.CoachingEpisodes.SingleAsync(candidate => candidate.Id == episodeId)).EndedAt!.Value;
        var accessPersistence = scope.ServiceProvider.GetRequiredService<IReportingRelationshipAccessPersistence>();
        var access = await accessPersistence.GetAccessAsync(
            trainer.Id.Rebind<LgymApi.Identity.Contracts.AccountReference>(),
            trainee.Id.Rebind<LgymApi.Identity.Contracts.AccountReference>());
        var occurredBeforeObservation = episodeEnd.AddYears(-10);

        access.RelationshipPeriods.Should().ContainSingle(period =>
            period.StartedAt == null
            && period.StartedAtProvenance == CoachingEpisodeStartProvenance.ExistingActiveLinkObservedDuringMigration);
        access.HadRelationshipAt(occurredBeforeObservation).Should().BeFalse();
        access.CanReadTrainerAuthoredRecordAt(occurredBeforeObservation).Should().BeTrue();
        access.CanReadTrainerAuthoredRecordAt(episodeEnd).Should().BeFalse();
    }

    [Test]
    public async Task ConcurrentInvitationAcceptance_AfterBothScopesObserveNoLink_PreservesOneActiveRelationship()
    {
        var (trainerOne, trainerTwo, trainee) = await SeedRelationshipUsersAsync("concurrent-accept");
        var firstInvitation = await AddPendingInvitationAsync(trainerOne.Id, trainee.Id, "first");
        var secondInvitation = await AddPendingInvitationAsync(trainerTwo.Id, trainee.Id, "second");
        var barrier = new TwoParticipantAsyncBarrier();

        var outcomes = await Task.WhenAll(
            RaceInvitationAcceptanceAsync(firstInvitation, trainerOne.Id, trainee.Id, barrier),
            RaceInvitationAcceptanceAsync(secondInvitation, trainerTwo.Id, trainee.Id, barrier));

        outcomes.Should().ContainSingle(outcome => outcome.Committed && !outcome.RecoveredDuplicate);
        outcomes.Should().ContainSingle(outcome => !outcome.Committed && outcome.RecoveredDuplicate);

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.TrainerTraineeLinks.CountAsync(link => link.TraineeId == trainee.Id)).Should().Be(1);
        (await database.CoachingEpisodes.CountAsync(episode => episode.TraineeId == trainee.Id)).Should().Be(1);
        (await database.TrainerInvitations.CountAsync(invitation =>
            (invitation.Id == firstInvitation || invitation.Id == secondInvitation)
            && invitation.Status == TrainerInvitationStatus.Accepted)).Should().Be(1);
    }

    private async Task<(User TrainerOne, User TrainerTwo, User Trainee)> SeedRelationshipUsersAsync(string scenario)
    {
        var suffix = Id<PostgreSqlCoachingEpisodeTests>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal);
        var trainerOne = await SeedUserAsync($"{scenario}-trainer-one-{suffix}", $"{scenario}-trainer-one-{suffix}@example.test");
        var trainerTwo = await SeedUserAsync($"{scenario}-trainer-two-{suffix}", $"{scenario}-trainer-two-{suffix}@example.test");
        var trainee = await SeedUserAsync($"{scenario}-trainee-{suffix}", $"{scenario}-trainee-{suffix}@example.test");
        return (trainerOne, trainerTwo, trainee);
    }

    private async Task<Id<CoachingEpisode>> AddActiveRelationshipAsync(Id<User> trainerId, Id<User> traineeId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activeLinks = scope.ServiceProvider.GetRequiredService<ICoachingActiveLinkPersistence>();
        var episodeId = Id<CoachingEpisode>.New();
        await activeLinks.AddAsync(new CoachingActiveLinkWriteModel(
            Id<TrainerTraineeLink>.New(),
            trainerId,
            traineeId,
            episodeId,
            DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        return episodeId;
    }

    private async Task UnlinkAsync(
        Id<User> actorId,
        Id<User> traineeId,
        CoachingEpisodeAuthorizationAction action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authorization = scope.ServiceProvider.GetRequiredService<ICoachingEpisodeAuthorizationService>();
        var activeLinks = scope.ServiceProvider.GetRequiredService<ICoachingActiveLinkPersistence>();
        var grant = action == CoachingEpisodeAuthorizationAction.TraineeDetach
            ? await authorization.AuthorizeTraineeDetachAndStageAsync(traineeId)
            : await authorization.AuthorizeAndStageAsync(actorId, actorId, traineeId, action);
        grant.Should().NotBeNull();
        await activeLinks.RemoveAsync(grant!);
        await database.SaveChangesAsync();
    }

    private async Task<Id<TrainerInvitation>> AddPendingInvitationAsync(
        Id<User> trainerId,
        Id<User> traineeId,
        string codePrefix)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invitation = new TrainerInvitation
        {
            Id = Id<TrainerInvitation>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            Code = $"{codePrefix}-{Id<TrainerInvitation>.New()}",
            Status = TrainerInvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        };
        database.TrainerInvitations.Add(invitation);
        await database.SaveChangesAsync();
        return invitation.Id;
    }

    private async Task<UnlinkRaceOutcome> RaceUnlinkAsync(
        Id<User> trainerId,
        Id<User> traineeId,
        TwoParticipantAsyncBarrier barrier)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authorization = scope.ServiceProvider.GetRequiredService<ICoachingEpisodeAuthorizationService>();
        var activeLinks = scope.ServiceProvider.GetRequiredService<ICoachingActiveLinkPersistence>();
        var grant = await authorization.AuthorizeAndStageAsync(
            trainerId,
            trainerId,
            traineeId,
            CoachingEpisodeAuthorizationAction.TrainerUnlink);
        grant.Should().NotBeNull();
        await activeLinks.RemoveAsync(grant!);
        await barrier.SignalAndWaitAsync();

        try
        {
            await database.SaveChangesAsync();
            return new UnlinkRaceOutcome(true, false);
        }
        catch (Exception commitFailure)
        {
            var recovered = await activeLinks.TryRecoverCompletedUnlinkAsync(grant!, commitFailure);
            return new UnlinkRaceOutcome(false, recovered);
        }
    }

    private async Task<InvitationRaceOutcome> RaceInvitationAcceptanceAsync(
        Id<TrainerInvitation> invitationId,
        Id<User> trainerId,
        Id<User> traineeId,
        TwoParticipantAsyncBarrier barrier)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activeLinks = scope.ServiceProvider.GetRequiredService<ICoachingActiveLinkPersistence>();
        (await activeLinks.HasForTraineeAsync(traineeId)).Should().BeFalse();
        var invitation = await database.TrainerInvitations.SingleAsync(candidate => candidate.Id == invitationId);
        var now = DateTimeOffset.UtcNow;
        await activeLinks.AddAsync(new CoachingActiveLinkWriteModel(
            Id<TrainerTraineeLink>.New(),
            trainerId,
            traineeId,
            Id<CoachingEpisode>.New(),
            now));
        invitation.Status = TrainerInvitationStatus.Accepted;
        invitation.RespondedAt = now;
        await barrier.SignalAndWaitAsync();

        try
        {
            await database.SaveChangesAsync();
            return new InvitationRaceOutcome(true, false);
        }
        catch (Exception commitFailure)
        {
            var recovered = await activeLinks.TryRecoverDuplicateLinkAsync(traineeId, commitFailure);
            return new InvitationRaceOutcome(false, recovered);
        }
    }

    private static ApiIdempotencyRecord CreateProbe(string idempotencyKey)
        => new()
        {
            Id = Id<ApiIdempotencyRecord>.New(),
            IdempotencyKey = idempotencyKey,
            ScopeTuple = $"POST|/postgresql-coaching-episode-tests|{idempotencyKey}",
            RequestFingerprint = new string('f', 64),
            ResponseStatusCode = 200,
            ResponseBodyJson = "{}",
            ProcessedAt = DateTimeOffset.UtcNow
        };

    private sealed record UnlinkRaceOutcome(bool Committed, bool Recovered);

    private sealed record InvitationRaceOutcome(bool Committed, bool RecoveredDuplicate);

    private sealed class TwoParticipantAsyncBarrier
    {
        private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public async Task SignalAndWaitAsync()
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _bothArrived.TrySetResult();
            }

            await _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
internal sealed class PostgreSqlCoachingEpisodeMigrationTests
{
    private const string PreviousMigrationId = "20261028200254_AddReportingPhotoTypeSnapshots";

    [Test]
    public async Task Migration_BackfillsUnknownStartWithoutInventingTimestamp_AndRefusesDowngrade()
    {
        await using var environment = await PostgreSqlTutorialRowSecurityTestEnvironment.CreateAsync(
            activate: false,
            targetMigration: PreviousMigrationId);
        await using var database = CreateDbContext(environment.RuntimeConnectionString);
        var trainer = await TestDataFactory.SeedUserAsync(database, "backfill-trainer", "backfill-trainer@example.test", "password123");
        var trainee = await TestDataFactory.SeedUserAsync(database, "backfill-trainee", "backfill-trainee@example.test", "password123");
        await database.SaveChangesAsync();
        var linkId = Id<TrainerTraineeLink>.New();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TrainerTraineeLinks" ("Id", "TrainerId", "TraineeId", "CreatedAt", "UpdatedAt", "IsDeleted")
            VALUES ({linkId.ToString()}::uuid, {trainer.Id.ToString()}::uuid, {trainee.Id.ToString()}::uuid, {DateTimeOffset.UtcNow.AddYears(-2)}, {DateTimeOffset.UtcNow}, FALSE)
            """);

        await database.Database.MigrateAsync();

        database.ChangeTracker.Clear();
        var link = await database.TrainerTraineeLinks.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == linkId);
        link.EpisodeId.Should().NotBeNull();
        var episode = await database.CoachingEpisodes.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == link.EpisodeId);
        episode.StartedAt.Should().BeNull();
        episode.StartedAtProvenance.Should().Be(CoachingEpisodeStartProvenance.ExistingActiveLinkObservedDuringMigration);

        var downgrade = () => database.Database.MigrateAsync(PreviousMigrationId);
        await downgrade.Should().ThrowAsync<NotSupportedException>();
        (await database.CoachingEpisodes.IgnoreQueryFilters().AnyAsync(candidate => candidate.Id == episode.Id)).Should().BeTrue();
    }

    private static AppDbContext CreateDbContext(string connectionString)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
}
