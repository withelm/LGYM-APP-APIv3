using FluentAssertions;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlRecurringReportProcessingTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task ProcessDueAssignmentsAsync_DeletedTemplate_WithCurrentSource_ClosesScheduleLifecycle()
    {
        var scenario = await SeedDeletedTemplateLifecycleScenarioAsync(relink: false);
        await using var processingScope = Factory.Services.CreateAsyncScope();
        var service = processingScope.ServiceProvider.GetRequiredService<IRecurringReportAssignmentService>();

        await service.ProcessDueAssignmentsAsync();

        await AssertDeletedTemplateLifecycleAsync(
            scenario,
            expectedActive: false,
            expectedRevision: 5,
            RecurringReportScheduleChangeStatus.Superseded,
            RecurringReportScheduleChangeStatus.Invalidated);
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_DeletedTemplate_AfterSameTrainerRelink_DeniesWithoutMutation()
    {
        var scenario = await SeedDeletedTemplateLifecycleScenarioAsync(relink: true);
        await using var processingScope = Factory.Services.CreateAsyncScope();
        var service = processingScope.ServiceProvider.GetRequiredService<IRecurringReportAssignmentService>();

        await service.ProcessDueAssignmentsAsync();

        await AssertDeletedTemplateLifecycleAsync(
            scenario,
            expectedActive: true,
            expectedRevision: 4,
            RecurringReportScheduleChangeStatus.Active,
            RecurringReportScheduleChangeStatus.Pending);
    }

    [Test]
    public async Task ProcessDueAssignmentsAsync_WhenFirstAssignmentSaveFails_RollsBackAndProcessesSecond()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var scenario = await SeedProcessingScenarioAsync(cancellationToken);

        await using var processingScope = Factory.Services.CreateAsyncScope();
        var serviceProvider = processingScope.ServiceProvider;
        var database = serviceProvider.GetRequiredService<AppDbContext>();
        var requestPersistence = new DuplicateFirstRequestPersistence(
            new ReportRequestSubmissionPersistenceRepository(database),
            database,
            scenario.FirstAssignmentId,
            scenario.FirstCurrentRequestId);
        var observedUnitOfWork = new ObservedUnitOfWork(serviceProvider.GetRequiredService<IUnitOfWork>());
        var service = new RecurringReportAssignmentService(
            serviceProvider.GetRequiredService<IReportTemplatePersistence>(),
            requestPersistence,
            serviceProvider.GetRequiredService<IRecurringReportAssignmentPersistence>(),
            serviceProvider.GetRequiredService<IReportingRelationshipAccessPersistence>(),
            serviceProvider.GetRequiredService<IMapper>(),
            serviceProvider.GetRequiredService<ICommandOutboxWriter>(),
            observedUnitOfWork,
            NullLogger<RecurringReportAssignmentService>.Instance);
        service.GetType().GetConstructors().Single().GetParameters()
            .Should().NotContain(parameter => parameter.ParameterType == typeof(ICommandDispatcher));

        await service.ProcessDueAssignmentsAsync(cancellationToken);

        observedUnitOfWork.SaveChangesCalls.Should().Be(2);
        observedUnitOfWork.CommitCalls.Should().Be(1);
        observedUnitOfWork.RollbackCalls.Should().Be(1);
        requestPersistence.FailedAttemptRequestId.Should().NotBeNull();

        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var assertionDatabase = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignments = await assertionDatabase.RecurringReportAssignments
            .AsNoTracking()
            .OrderBy(assignment => assignment.CreatedAt)
            .ToListAsync(cancellationToken);
        var requests = await assertionDatabase.ReportRequests
            .AsNoTracking()
            .OrderBy(request => request.CreatedAt)
            .ToListAsync(cancellationToken);
        var envelopes = await assertionDatabase.CommandEnvelopes
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        assignments.Should().HaveCount(2);
        assignments.Single(assignment => assignment.Id == scenario.FirstAssignmentId)
            .CurrentReportRequestId.Should().Be(scenario.FirstCurrentRequestId);
        var secondAssignment = assignments.Single(assignment => assignment.Id == scenario.SecondAssignmentId);
        secondAssignment.CurrentReportRequestId.Should().NotBe(scenario.SecondCurrentRequestId);
        requests.Should().HaveCount(3);
        requests.Should().NotContain(request => request.Id == requestPersistence.FailedAttemptRequestId);
        requests.Should().ContainSingle(request => request.Id == secondAssignment.CurrentReportRequestId);
        envelopes.Should().ContainSingle();
        envelopes[0].CommandTypeFullName.Should().Be(
            "LgymApi.BackgroundWorker.Common.Commands.ReportRequestCreatedInAppNotificationCommand");
        envelopes[0].PayloadJson.Should().Contain(secondAssignment.CurrentReportRequestId!.Value.ToString());
        envelopes[0].PayloadJson.Should().NotContain(requestPersistence.FailedAttemptRequestId!.Value.ToString());
    }

    [Test]
    public async Task FindByIdForUpdateAsync_SecondTransactionWaitsThenReloadsCommittedCompleteGraph()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var assignmentId = await SeedAssignmentAsync(cancellationToken);

        await using var lockingScope = Factory.Services.CreateAsyncScope();
        var lockingDatabase = lockingScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lockingRepository = new RecurringReportAssignmentPersistenceRepository(lockingDatabase);
        await using var lockingTransaction = await lockingDatabase.Database.BeginTransactionAsync(cancellationToken);
        AssertCompleteGraph(await lockingRepository.FindByIdForUpdateAsync(assignmentId, cancellationToken));

        var assignment = await lockingDatabase.RecurringReportAssignments
            .SingleAsync(candidate => candidate.Id == assignmentId, cancellationToken);
        assignment.Note = "committed by locking transaction";
        await lockingDatabase.SaveChangesAsync(cancellationToken);

        await using var blockedScope = Factory.Services.CreateAsyncScope();
        var blockedDatabase = blockedScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var blockedRepository = new RecurringReportAssignmentPersistenceRepository(blockedDatabase);
        await using var blockedTransaction = await blockedDatabase.Database.BeginTransactionAsync(cancellationToken);
        var blockedReload = blockedRepository.FindByIdForUpdateAsync(assignmentId, cancellationToken);

        var completionBeforeRelease = async () =>
            await blockedReload.WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);

        await completionBeforeRelease.Should().ThrowAsync<TimeoutException>();

        await lockingTransaction.CommitAsync(cancellationToken);

        var blockedResult = await blockedReload.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        blockedResult!.Note.Should().Be("committed by locking transaction");
        AssertCompleteGraph(blockedResult);
        await blockedTransaction.CommitAsync(cancellationToken);
    }

    [Test]
    public async Task FeedbackReadRead_SecondSubmissionLockWaitsUntilFirstReadTransactionCommits()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var assignmentId = await SeedAssignmentAsync(cancellationToken);
        var scenario = await LoadFeedbackScenarioAsync(assignmentId, cancellationToken);

        await using var firstScope = Factory.Services.CreateAsyncScope();
        var firstDatabase = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstRepository = new ReportRequestSubmissionPersistenceRepository(firstDatabase);
        await using var firstTransaction = await firstDatabase.Database.BeginTransactionAsync(cancellationToken);
        (await firstRepository.FindSubmissionForTraineeForUpdateAsync(scenario.SubmissionId, scenario.TraineeId, cancellationToken)).Should().NotBeNull();

        await using var secondScope = Factory.Services.CreateAsyncScope();
        var secondDatabase = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondRepository = new ReportRequestSubmissionPersistenceRepository(secondDatabase);
        await using var secondTransaction = await secondDatabase.Database.BeginTransactionAsync(cancellationToken);
        var secondRead = secondRepository.FindSubmissionForTraineeForUpdateAsync(scenario.SubmissionId, scenario.TraineeId, cancellationToken);

        Func<Task> waitForSecondRead = () => secondRead.WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
        await waitForSecondRead.Should().ThrowAsync<TimeoutException>();
        await firstTransaction.CommitAsync(cancellationToken);
        (await secondRead.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)).Should().NotBeNull();
        await secondTransaction.CommitAsync(cancellationToken);
    }

    [Test]
    public async Task FeedbackReadEdit_TrainerEditLockWaitsUntilFeedbackReadTransactionCommits()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var assignmentId = await SeedAssignmentAsync(cancellationToken);
        var scenario = await LoadFeedbackScenarioAsync(assignmentId, cancellationToken);

        await using var readScope = Factory.Services.CreateAsyncScope();
        var readDatabase = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var readRepository = new ReportRequestSubmissionPersistenceRepository(readDatabase);
        await using var readTransaction = await readDatabase.Database.BeginTransactionAsync(cancellationToken);
        (await readRepository.FindSubmissionForTraineeForUpdateAsync(scenario.SubmissionId, scenario.TraineeId, cancellationToken)).Should().NotBeNull();

        await using var editScope = Factory.Services.CreateAsyncScope();
        var editDatabase = editScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var editRepository = new ReportRequestSubmissionPersistenceRepository(editDatabase);
        await using var editTransaction = await editDatabase.Database.BeginTransactionAsync(cancellationToken);
        var edit = editRepository.FindSubmissionForTrainerForUpdateAsync(scenario.SubmissionId, scenario.TrainerId, scenario.TraineeId, cancellationToken);

        Func<Task> waitForEdit = () => edit.WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
        await waitForEdit.Should().ThrowAsync<TimeoutException>();
        await readTransaction.CommitAsync(cancellationToken);
        (await edit.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)).Should().NotBeNull();
        await editTransaction.CommitAsync(cancellationToken);
    }

    private async Task<FeedbackScenario> LoadFeedbackScenarioAsync(
        Id<RecurringReportAssignment> assignmentId,
        CancellationToken cancellationToken)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await database.RecurringReportAssignments
            .AsNoTracking()
            .Include(candidate => candidate.CurrentReportRequest)
                .ThenInclude(request => request!.Submission)
            .SingleAsync(candidate => candidate.Id == assignmentId, cancellationToken);
        return new FeedbackScenario(
            assignment.CurrentReportRequest!.Submission!.Id,
            assignment.TrainerId.Rebind<LgymApi.Identity.Contracts.AccountReference>(),
            assignment.TraineeId.Rebind<LgymApi.Identity.Contracts.AccountReference>());
    }

    private async Task<Id<RecurringReportAssignment>> SeedAssignmentAsync(CancellationToken cancellationToken)
    {
        var trainer = await SeedUserAsync(
            name: $"recurring-lock-trainer-{Id<User>.New()}",
            email: $"recurring-lock-trainer-{Id<User>.New()}@example.com");
        var trainee = await SeedUserAsync(
            name: $"recurring-lock-trainee-{Id<User>.New()}",
            email: $"recurring-lock-trainee-{Id<User>.New()}@example.com");
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Weekly report",
            Fields =
            [
                new ReportTemplateField { Id = Id<ReportTemplateField>.New(), Key = "summary", Order = 2 },
                new ReportTemplateField { Id = Id<ReportTemplateField>.New(), Key = "mood", Order = 1 }
            ]
        };
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Submitted
        };
        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = request.Id,
            ReportRequest = request,
            TraineeId = trainee.Id,
            PayloadJson = "{}",
            TrainerFeedbackAddedAt = DateTimeOffset.UtcNow.AddDays(-2),
            TrainerFeedbackReadAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        request.Submission = submission;
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-7),
            IsActive = true,
            CurrentReportRequestId = request.Id,
            CurrentReportRequest = request
        };
        database.AddRange(template, request, submission, assignment);
        await database.SaveChangesAsync(cancellationToken);
        return assignment.Id;
    }

    private async Task<ProcessingScenario> SeedProcessingScenarioAsync(CancellationToken cancellationToken)
    {
        var trainer = await SeedUserAsync(
            name: $"recurring-processing-trainer-{Id<User>.New()}",
            email: $"recurring-processing-trainer-{Id<User>.New()}@example.com");
        var trainee = await SeedUserAsync(
            name: $"recurring-processing-trainee-{Id<User>.New()}",
            email: $"recurring-processing-trainee-{Id<User>.New()}@example.com");
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Recurring isolation template"
        };
        var now = DateTimeOffset.UtcNow;
        var episode = new CoachingEpisode
        {
            Id = Id<CoachingEpisode>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            StartedAt = now.AddDays(-10),
            StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
        };
        var first = CreateDueAssignmentGraph(
            trainer.Id,
            trainee.Id,
            template,
            nextEligibleAt: now.AddDays(-2),
            createdAt: now.AddDays(-4));
        var second = CreateDueAssignmentGraph(
            trainer.Id,
            trainee.Id,
            template,
            nextEligibleAt: now.AddDays(-1),
            createdAt: now.AddDays(-3));
        first.Assignment.SourceCoachingEpisodeId = episode.Id;
        second.Assignment.SourceCoachingEpisodeId = episode.Id;
        var activeLink = new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            EpisodeId = episode.Id
        };

        database.AddRange(
            template,
            first.Request,
            first.Submission,
            first.Assignment,
            second.Request,
            second.Submission,
            second.Assignment,
            episode,
            activeLink);
        await database.SaveChangesAsync(cancellationToken);

        return new ProcessingScenario(
            first.Assignment.Id,
            first.Request.Id,
            second.Assignment.Id,
            second.Request.Id);
    }

    private async Task<DeletedTemplateLifecycleScenario> SeedDeletedTemplateLifecycleScenarioAsync(bool relink)
    {
        var trainer = await SeedUserAsync(
            name: $"deleted-template-trainer-{Id<User>.New()}",
            email: $"deleted-template-trainer-{Id<User>.New()}@example.com");
        var trainee = await SeedUserAsync(
            name: $"deleted-template-trainee-{Id<User>.New()}",
            email: $"deleted-template-trainee-{Id<User>.New()}@example.com");
        var now = DateTimeOffset.UtcNow;
        var episode = new CoachingEpisode
        {
            Id = Id<CoachingEpisode>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            StartedAt = now.AddDays(-10),
            StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
        };
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Deleted lifecycle template",
            IsDeleted = true
        };
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = now.AddDays(-7),
            IsActive = true,
            NextEligibleAt = now.AddDays(-1),
            ScheduleRevision = 4,
            SourceCoachingEpisodeId = episode.Id
        };
        var activeOverride = new RecurringReportScheduleChange
        {
            Id = Id<RecurringReportScheduleChange>.New(),
            RecurringReportAssignmentId = assignment.Id,
            RecurringReportAssignment = assignment,
            Kind = RecurringReportScheduleChangeKind.TrainerOverride,
            Status = RecurringReportScheduleChangeStatus.Active,
            ScheduleRevision = 4,
            OldScheduledAt = now.AddDays(-1),
            ProposedScheduledAt = now.AddDays(2),
            RequestedByUserId = trainer.Id,
            RequestedAt = now.AddHours(-2)
        };
        var pendingProposal = new RecurringReportScheduleChange
        {
            Id = Id<RecurringReportScheduleChange>.New(),
            RecurringReportAssignmentId = assignment.Id,
            RecurringReportAssignment = assignment,
            Kind = RecurringReportScheduleChangeKind.TraineeProposal,
            Status = RecurringReportScheduleChangeStatus.Pending,
            ScheduleRevision = 4,
            OldScheduledAt = now.AddDays(2),
            ProposedScheduledAt = now.AddDays(3),
            RequestedByUserId = trainee.Id,
            RequestedAt = now.AddHours(-1)
        };
        assignment.ScheduleChanges.Add(activeOverride);
        assignment.ScheduleChanges.Add(pendingProposal);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.AddRange(
                episode,
                new TrainerTraineeLink
                {
                    Id = Id<TrainerTraineeLink>.New(),
                    TrainerId = trainer.Id,
                    TraineeId = trainee.Id,
                    EpisodeId = episode.Id
                },
                template,
                assignment,
                activeOverride,
                pendingProposal);
            await database.SaveChangesAsync();
        }

        if (relink)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var link = await database.TrainerTraineeLinks.SingleAsync(candidate => candidate.TraineeId == trainee.Id);
            database.TrainerTraineeLinks.Remove(link);
            await database.SaveChangesAsync();
            var replacementEpisode = new CoachingEpisode
            {
                Id = Id<CoachingEpisode>.New(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id,
                StartedAt = DateTimeOffset.UtcNow,
                StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
            };
            database.AddRange(
                replacementEpisode,
                new TrainerTraineeLink
                {
                    Id = Id<TrainerTraineeLink>.New(),
                    TrainerId = trainer.Id,
                    TraineeId = trainee.Id,
                    EpisodeId = replacementEpisode.Id
                });
            await database.SaveChangesAsync();
        }

        return new DeletedTemplateLifecycleScenario(assignment.Id, activeOverride.Id, pendingProposal.Id);
    }

    private async Task AssertDeletedTemplateLifecycleAsync(
        DeletedTemplateLifecycleScenario scenario,
        bool expectedActive,
        int expectedRevision,
        RecurringReportScheduleChangeStatus expectedOverrideStatus,
        RecurringReportScheduleChangeStatus expectedProposalStatus)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await database.RecurringReportAssignments.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == scenario.AssignmentId);
        var changes = await database.RecurringReportScheduleChanges.AsNoTracking()
            .Where(change => change.RecurringReportAssignmentId == scenario.AssignmentId)
            .ToListAsync();
        assignment.IsActive.Should().Be(expectedActive);
        assignment.ScheduleRevision.Should().Be(expectedRevision);
        changes.Single(change => change.Id == scenario.OverrideId).Status.Should().Be(expectedOverrideStatus);
        changes.Single(change => change.Id == scenario.ProposalId).Status.Should().Be(expectedProposalStatus);
    }

    private static AssignmentGraph CreateDueAssignmentGraph(
        Id<User> trainerId,
        Id<User> traineeId,
        ReportTemplate template,
        DateTimeOffset nextEligibleAt,
        DateTimeOffset createdAt)
    {
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Submitted,
            SubmittedAt = createdAt.AddDays(1),
            CreatedAt = createdAt
        };
        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = request.Id,
            ReportRequest = request,
            TraineeId = traineeId,
            PayloadJson = "{}",
            TrainerFeedbackAddedAt = createdAt.AddDays(2),
            TrainerFeedbackReadAt = createdAt.AddDays(3),
            CreatedAt = createdAt.AddDays(1)
        };
        request.Submission = submission;
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = createdAt,
            IsActive = true,
            CurrentReportRequestId = request.Id,
            CurrentReportRequest = request,
            NextEligibleAt = nextEligibleAt,
            CreatedAt = createdAt
        };
        return new AssignmentGraph(assignment, request, submission);
    }

    private static void AssertCompleteGraph(RecurringReportAssignmentPersistenceModel? result)
    {
        result.Should().NotBeNull();
        result!.IsDeleted.Should().BeFalse();
        result.Template.Fields.Select(field => field.Order).Should().BeInAscendingOrder();
        result.CurrentReportRequest.Should().NotBeNull();
        result.CurrentReportRequest!.Template.Fields.Select(field => field.Order).Should().BeInAscendingOrder();
        result.CurrentReportRequest.Submission.Should().NotBeNull();
    }

    private sealed record ProcessingScenario(
        Id<RecurringReportAssignment> FirstAssignmentId,
        Id<ReportRequest> FirstCurrentRequestId,
        Id<RecurringReportAssignment> SecondAssignmentId,
        Id<ReportRequest> SecondCurrentRequestId);

    private sealed record DeletedTemplateLifecycleScenario(
        Id<RecurringReportAssignment> AssignmentId,
        Id<RecurringReportScheduleChange> OverrideId,
        Id<RecurringReportScheduleChange> ProposalId);

    private sealed record AssignmentGraph(
        RecurringReportAssignment Assignment,
        ReportRequest Request,
        ReportSubmission Submission);

    private sealed record FeedbackScenario(
        Id<ReportSubmission> SubmissionId,
        Id<LgymApi.Identity.Contracts.AccountReference> TrainerId,
        Id<LgymApi.Identity.Contracts.AccountReference> TraineeId);

    private sealed class DuplicateFirstRequestPersistence(
        IReportRequestSubmissionPersistence inner,
        AppDbContext database,
        Id<RecurringReportAssignment> failingAssignmentId,
        Id<ReportRequest> duplicateRequestId) : IReportRequestSubmissionPersistence
    {
        public Id<ReportRequest>? FailedAttemptRequestId { get; private set; }

        public Task AddRequestAsync(NewReportRequestPersistenceModel request, CancellationToken cancellationToken = default)
        {
            if (request.RecurringReportAssignmentId == failingAssignmentId)
            {
                FailedAttemptRequestId = request.Id;
                request = request with { Id = duplicateRequestId };
                database.SavingChanges += PreserveExistingPointerForDuplicateFailure;
            }

            return inner.AddRequestAsync(request, cancellationToken);
        }

        private void PreserveExistingPointerForDuplicateFailure(object? sender, SavingChangesEventArgs eventArgs)
        {
            var assignment = database.ChangeTracker
                .Entries<RecurringReportAssignment>()
                .SingleOrDefault(entry => entry.Entity.Id == failingAssignmentId);
            if (assignment is not null)
            {
                assignment.Entity.CurrentReportRequestId = duplicateRequestId;
            }

            database.SavingChanges -= PreserveExistingPointerForDuplicateFailure;
        }

        public Task<ReportRequestPersistenceModel?> FindRequestByIdAsync(Id<ReportRequest> requestId, CancellationToken cancellationToken = default)
            => inner.FindRequestByIdAsync(requestId, cancellationToken);

        public Task<IReadOnlyList<ReportRequestPersistenceModel>> ListPendingOrExpiredByTraineeAsync(Id<LgymApi.Identity.Contracts.AccountReference> traineeId, CancellationToken cancellationToken = default)
            => inner.ListPendingOrExpiredByTraineeAsync(traineeId, cancellationToken);

        public Task SetRequestExpiredAsync(Id<ReportRequest> requestId, CancellationToken cancellationToken = default)
            => inner.SetRequestExpiredAsync(requestId, cancellationToken);

        public Task SetRequestSubmittedAsync(Id<ReportRequest> requestId, DateTimeOffset submittedAt, CancellationToken cancellationToken = default)
            => inner.SetRequestSubmittedAsync(requestId, submittedAt, cancellationToken);

        public Task SetRequestCompletedAsync(Id<ReportRequest> requestId, CancellationToken cancellationToken = default)
            => inner.SetRequestCompletedAsync(requestId, cancellationToken);

        public Task AddSubmissionAsync(NewReportSubmissionPersistenceModel submission, CancellationToken cancellationToken = default)
            => inner.AddSubmissionAsync(submission, cancellationToken);

        public Task<ReportSubmissionPersistenceModel?> FindSubmissionForTrainerAsync(Id<ReportSubmission> submissionId, Id<LgymApi.Identity.Contracts.AccountReference> trainerId, Id<LgymApi.Identity.Contracts.AccountReference> traineeId, CancellationToken cancellationToken = default)
            => inner.FindSubmissionForTrainerAsync(submissionId, trainerId, traineeId, cancellationToken);

        public Task<ReportSubmissionPersistenceModel?> FindSubmissionForTraineeAsync(Id<ReportSubmission> submissionId, Id<LgymApi.Identity.Contracts.AccountReference> traineeId, CancellationToken cancellationToken = default)
            => inner.FindSubmissionForTraineeAsync(submissionId, traineeId, cancellationToken);

        public Task<IReadOnlyList<ReportSubmissionPersistenceModel>> ListSubmissionsByTraineeAsync(Id<LgymApi.Identity.Contracts.AccountReference> traineeId, CancellationToken cancellationToken = default)
            => inner.ListSubmissionsByTraineeAsync(traineeId, cancellationToken);

        public Task<IReadOnlyList<ReportSubmissionPersistenceModel>> ListSubmissionsByTrainerAndTraineeAsync(Id<LgymApi.Identity.Contracts.AccountReference> trainerId, Id<LgymApi.Identity.Contracts.AccountReference> traineeId, CancellationToken cancellationToken = default)
            => inner.ListSubmissionsByTrainerAndTraineeAsync(trainerId, traineeId, cancellationToken);

        public Task UpdateFeedbackAsync(Id<ReportSubmission> submissionId, ReportSubmissionFeedbackUpdatePersistenceModel update, CancellationToken cancellationToken = default)
            => inner.UpdateFeedbackAsync(submissionId, update, cancellationToken);

        public Task UpdateAnswersAsync(Id<ReportSubmission> submissionId, string payloadJson, CancellationToken cancellationToken = default)
            => inner.UpdateAnswersAsync(submissionId, payloadJson, cancellationToken);

        public Task<ReportSubmissionPersistenceModel?> FindSubmissionForTraineeForUpdateAsync(Id<ReportSubmission> submissionId, Id<LgymApi.Identity.Contracts.AccountReference> traineeId, CancellationToken cancellationToken = default)
            => inner.FindSubmissionForTraineeForUpdateAsync(submissionId, traineeId, cancellationToken);

        public Task<ReportSubmissionPersistenceModel?> FindSubmissionForTrainerForUpdateAsync(Id<ReportSubmission> submissionId, Id<LgymApi.Identity.Contracts.AccountReference> trainerId, Id<LgymApi.Identity.Contracts.AccountReference> traineeId, CancellationToken cancellationToken = default)
            => inner.FindSubmissionForTrainerForUpdateAsync(submissionId, trainerId, traineeId, cancellationToken);
    }
}
