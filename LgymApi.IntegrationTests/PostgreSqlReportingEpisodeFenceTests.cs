using System.Net;
using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Options;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.Authorization;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Infrastructure.Data;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlReportingEpisodeFenceTests : PostgreSqlRecurringReportHttpTestBase
{
    private const string FeedbackAddedCommandId =
        "LgymApi.BackgroundWorker.Common.Commands.ReportFeedbackAddedInAppNotificationCommand";
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    [Test]
    public async Task FeedbackWrite_AfterTrainerReplacementAndSameTrainerRelink_DeniesWithoutMutation()
    {
        var scenario = await SeedFeedbackScenarioAsync("feedback-relink");
        await RelinkAsync(scenario.Trainee.Id, scenario.ReplacementTrainer.Id);
        var feedback = new
        {
            trainerOverallComment = "Cross-episode feedback",
            trainerFieldComments = new Dictionary<string, string?> { ["note"] = "Must not persist" }
        };

        SetAuthorizationHeader(scenario.ReplacementTrainer.Id);
        using var replacementResponse = await PostAsJsonWithApiOptionsAsync(FeedbackUrl(scenario), feedback);
        replacementResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        SetAuthorizationHeader(scenario.SourceTrainer.Id);
        using var formerResponse = await PostAsJsonWithApiOptionsAsync(FeedbackUrl(scenario), feedback);
        formerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await RelinkAsync(scenario.Trainee.Id, scenario.SourceTrainer.Id);
        SetAuthorizationHeader(scenario.SourceTrainer.Id);
        using var relinkedResponse = await PostAsJsonWithApiOptionsAsync(FeedbackUrl(scenario), feedback);
        relinkedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AssertFeedbackUnchangedAsync(scenario);
    }

    [Test]
    public async Task FeedbackWrite_ConcurrentWithUnlink_RollsBackFeedbackAndRecurringScheduleMutation()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var scenario = await SeedFeedbackScenarioAsync("feedback-unlink-race", withExistingFeedback: true);

        await using var feedbackScope = Factory.Services.CreateAsyncScope();
        var serviceProvider = feedbackScope.ServiceProvider;
        var gatedUnitOfWork = new GatedSaveUnitOfWork(serviceProvider.GetRequiredService<IUnitOfWork>());
        var service = CreateReportingService(serviceProvider, gatedUnitOfWork);
        var feedbackTask = service.UpdateTrainerFeedbackAsync(
            TrainerAccount(scenario.SourceTrainer.Id),
            scenario.Trainee.Id.Rebind<AccountReference>(),
            scenario.SubmissionId,
            new UpdateReportSubmissionFeedbackCommand
            {
                TrainerOverallComment = null,
                FieldComments = []
            },
            cancellationToken);

        await gatedUnitOfWork.SaveReached.Task.WaitAsync(GateTimeout, cancellationToken);
        await UnlinkAsync(scenario.Trainee.Id, cancellationToken);
        gatedUnitOfWork.ReleaseSave.TrySetResult(true).Should().BeTrue();
        var result = await feedbackTask.WaitAsync(GateTimeout, cancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var database = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submission = await database.ReportSubmissions.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == scenario.SubmissionId, cancellationToken);
        var assignment = await database.RecurringReportAssignments.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == scenario.AssignmentId, cancellationToken);
        submission.TrainerOverallComment.Should().Be("Original feedback");
        submission.TrainerFeedbackAddedAt.Should().NotBeNull();
        assignment.NextEligibleAt.Should().BeCloseTo(
            scenario.OriginalNextEligibleAt,
            TimeSpan.FromMicroseconds(1));
        (await database.TrainerTraineeLinks.AnyAsync(link => link.TraineeId == scenario.Trainee.Id, cancellationToken))
            .Should().BeFalse();
    }

    [Test]
    public async Task FeedbackWrite_NonemptyConcurrentWithUnlink_ReturnsNotFoundWithoutWritesOrEnvelope()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var scenario = await SeedFeedbackScenarioAsync("feedback-added-unlink-race");
        var originalEnvelopeCount = await CountFeedbackAddedEnvelopesAsync(cancellationToken);

        await using var feedbackScope = Factory.Services.CreateAsyncScope();
        var serviceProvider = feedbackScope.ServiceProvider;
        var gatedRelationshipAccess = new GatedRelationshipAccessPersistence(
            serviceProvider.GetRequiredService<IReportingRelationshipAccessPersistence>());
        var service = CreateReportingService(
            serviceProvider,
            serviceProvider.GetRequiredService<IUnitOfWork>(),
            gatedRelationshipAccess);
        var feedbackTask = service.UpdateTrainerFeedbackAsync(
            TrainerAccount(scenario.SourceTrainer.Id),
            scenario.Trainee.Id.Rebind<AccountReference>(),
            scenario.SubmissionId,
            new UpdateReportSubmissionFeedbackCommand
            {
                TrainerOverallComment = "New feedback",
                FieldComments = new Dictionary<string, string?> { ["note"] = "Must roll back" }
            },
            cancellationToken);

        await gatedRelationshipAccess.RevisionCheckStaged.Task.WaitAsync(GateTimeout, cancellationToken);
        await UnlinkAsync(scenario.Trainee.Id, cancellationToken);
        gatedRelationshipAccess.Release.TrySetResult(true).Should().BeTrue();
        var result = await feedbackTask.WaitAsync(GateTimeout, cancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
        await AssertFeedbackUnchangedAsync(scenario);
        (await CountFeedbackAddedEnvelopesAsync(cancellationToken)).Should().Be(originalEnvelopeCount);
    }

    [Test]
    public async Task SubmitLegacyNullSource_AfterRelink_DeniesWithoutInventingProvenance_AndExplicitSourceSucceeds()
    {
        var scenario = await SeedLegacySubmissionScenarioAsync();
        var currentEpisodeId = await RelinkAsync(scenario.Trainee.Id, scenario.Trainer.Id);
        var explicitRequestId = await AddPendingRequestAsync(
            scenario.Trainer.Id,
            scenario.Trainee.Id,
            scenario.TemplateId,
            currentEpisodeId);
        SetAuthorizationHeader(scenario.Trainee.Id);

        using var legacyResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainee/report-requests/{scenario.LegacyRequestId}/submit",
            new { answers = new { note = "Ambiguous legacy source" } });
        legacyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var explicitResponse = await PostAsJsonWithApiOptionsAsync(
            $"/api/trainee/report-requests/{explicitRequestId}/submit",
            new { answers = new { note = "Current explicit source" } });
        explicitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var legacyRequest = await database.ReportRequests.AsNoTracking()
            .Include(request => request.Submission)
            .SingleAsync(request => request.Id == scenario.LegacyRequestId);
        var explicitRequest = await database.ReportRequests.AsNoTracking()
            .Include(request => request.Submission)
            .SingleAsync(request => request.Id == explicitRequestId);
        legacyRequest.Status.Should().Be(ReportRequestStatus.Pending);
        legacyRequest.Submission.Should().BeNull();
        explicitRequest.Status.Should().Be(ReportRequestStatus.Submitted);
        explicitRequest.Submission.Should().NotBeNull();
        explicitRequest.Submission!.SourceCoachingEpisodeId.Should().Be(currentEpisodeId);
    }

    private async Task<FeedbackScenario> SeedFeedbackScenarioAsync(
        string prefix,
        bool withExistingFeedback = false)
    {
        var sourceTrainer = await SeedUserAsync($"{prefix}-source", $"{prefix}-source@example.test");
        var replacementTrainer = await SeedUserAsync($"{prefix}-replacement", $"{prefix}-replacement@example.test");
        var trainee = await SeedUserAsync($"{prefix}-trainee", $"{prefix}-trainee@example.test");
        var now = DateTimeOffset.UtcNow;
        var episode = Episode(sourceTrainer.Id, trainee.Id, now.AddDays(-7));
        var template = Template(sourceTrainer.Id, $"{prefix} template");
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = sourceTrainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Submitted,
            SubmittedAt = now.AddDays(-1),
            SourceCoachingEpisodeId = episode.Id
        };
        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = request.Id,
            ReportRequest = request,
            TraineeId = trainee.Id,
            PayloadJson = "{\"note\":\"Submitted\"}",
            SourceCoachingEpisodeId = episode.Id,
            AcceptedAt = now.AddDays(-1),
            TrainerOverallComment = withExistingFeedback ? "Original feedback" : null,
            TrainerFeedbackAddedAt = withExistingFeedback ? now.AddHours(-1) : null
        };
        request.Submission = submission;
        var nextEligibleAt = now.AddDays(3);
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = sourceTrainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Template = template,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = now.AddDays(-7),
            IsActive = true,
            CurrentReportRequestId = request.Id,
            CurrentReportRequest = request,
            NextEligibleAt = nextEligibleAt,
            SourceCoachingEpisodeId = episode.Id
        };

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.UserRoles.AddRange(
            LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, sourceTrainer.Id),
            LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, replacementTrainer.Id));
        database.AddRange(
            episode,
            ActiveLink(sourceTrainer.Id, trainee.Id, episode.Id),
            template,
            request,
            submission,
            assignment);
        await database.SaveChangesAsync();
        return new FeedbackScenario(
            sourceTrainer,
            replacementTrainer,
            trainee,
            submission.Id,
            assignment.Id,
            nextEligibleAt);
    }

    private async Task<LegacySubmissionScenario> SeedLegacySubmissionScenarioAsync()
    {
        var trainer = await SeedUserAsync("legacy-source-trainer", "legacy-source-trainer@example.test");
        var trainee = await SeedUserAsync("legacy-source-trainee", "legacy-source-trainee@example.test");
        var episode = Episode(trainer.Id, trainee.Id, DateTimeOffset.UtcNow.AddDays(-7));
        var template = Template(trainer.Id, "Legacy source template");
        var request = PendingRequest(trainer.Id, trainee.Id, template, sourceEpisodeId: null);

        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
        database.AddRange(episode, ActiveLink(trainer.Id, trainee.Id, episode.Id), template, request);
        await database.SaveChangesAsync();
        return new LegacySubmissionScenario(trainer, trainee, template.Id, request.Id);
    }

    private async Task<Id<ReportRequest>> AddPendingRequestAsync(
        Id<User> trainerId,
        Id<User> traineeId,
        Id<ReportTemplate> templateId,
        Id<CoachingEpisode> sourceEpisodeId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = await database.ReportTemplates
            .Include(candidate => candidate.Fields)
            .SingleAsync(candidate => candidate.Id == templateId);
        var request = PendingRequest(trainerId, traineeId, template, sourceEpisodeId);
        database.ReportRequests.Add(request);
        await database.SaveChangesAsync();
        return request.Id;
    }

    private async Task<Id<CoachingEpisode>> RelinkAsync(Id<User> traineeId, Id<User> trainerId)
    {
        await UnlinkAsync(traineeId, CancellationToken.None);
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var episode = Episode(trainerId, traineeId, DateTimeOffset.UtcNow);
        database.AddRange(episode, ActiveLink(trainerId, traineeId, episode.Id));
        await database.SaveChangesAsync();
        return episode.Id;
    }

    private async Task UnlinkAsync(Id<User> traineeId, CancellationToken cancellationToken)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var link = await database.TrainerTraineeLinks.SingleAsync(
            candidate => candidate.TraineeId == traineeId,
            cancellationToken);
        database.TrainerTraineeLinks.Remove(link);
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task AssertFeedbackUnchangedAsync(FeedbackScenario scenario)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submission = await database.ReportSubmissions.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == scenario.SubmissionId);
        var assignment = await database.RecurringReportAssignments.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == scenario.AssignmentId);
        submission.TrainerOverallComment.Should().BeNull();
        submission.TrainerFieldCommentsJson.Should().BeNull();
        submission.TrainerFeedbackAddedAt.Should().BeNull();
        assignment.NextEligibleAt.Should().BeCloseTo(
            scenario.OriginalNextEligibleAt,
            TimeSpan.FromMicroseconds(1));
    }

    private async Task<int> CountFeedbackAddedEnvelopesAsync(CancellationToken cancellationToken)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await database.CommandEnvelopes.CountAsync(
            envelope => envelope.CommandTypeFullName == FeedbackAddedCommandId,
            cancellationToken);
    }

    private static ReportingService CreateReportingService(
        IServiceProvider serviceProvider,
        IUnitOfWork unitOfWork,
        IReportingRelationshipAccessPersistence? relationshipAccessPersistence = null)
        => new(
            serviceProvider.GetRequiredService<IReportTemplatePersistence>(),
            serviceProvider.GetRequiredService<IReportRequestSubmissionPersistence>(),
            serviceProvider.GetRequiredService<IRecurringReportAssignmentPersistence>(),
            serviceProvider.GetRequiredService<IReportPhotoPersistence>(),
            relationshipAccessPersistence
                ?? serviceProvider.GetRequiredService<IReportingRelationshipAccessPersistence>(),
            serviceProvider.GetRequiredService<IReportingAuthorizationCoordinator>(),
            serviceProvider.GetRequiredService<IAccountLookupService>(),
            serviceProvider.GetRequiredService<IReportSubmissionAcceptedProgressCommandFactory>(),
            serviceProvider.GetRequiredService<ICommandDispatcher>(),
            serviceProvider.GetRequiredService<ICommandOutboxWriter>(),
            unitOfWork,
            serviceProvider.GetRequiredService<IPhotoStorageProvider>(),
            serviceProvider.GetRequiredService<IMapper>(),
            serviceProvider.GetRequiredService<ILogger<ReportingService>>(),
            serviceProvider.GetRequiredService<PhotoStorageOptions>());

    private static AuthenticatedAccountContext TrainerAccount(Id<User> trainerId)
        => new(
            trainerId.Rebind<AccountReference>(),
            null,
            [AuthConstants.Roles.Trainer],
            [],
            false,
            false);

    private static CoachingEpisode Episode(Id<User> trainerId, Id<User> traineeId, DateTimeOffset startedAt)
        => new()
        {
            Id = Id<CoachingEpisode>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            StartedAt = startedAt,
            StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
        };

    private static TrainerTraineeLink ActiveLink(
        Id<User> trainerId,
        Id<User> traineeId,
        Id<CoachingEpisode> episodeId)
        => new()
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            EpisodeId = episodeId
        };

    private static ReportTemplate Template(Id<User> trainerId, string name)
    {
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainerId,
            Name = name
        };
        template.Fields.Add(new ReportTemplateField
        {
            Id = Id<ReportTemplateField>.New(),
            TemplateId = template.Id,
            Key = "note",
            Label = "Note",
            Type = ReportFieldType.Text,
            IsRequired = true,
            Order = 0
        });
        return template;
    }

    private static ReportRequest PendingRequest(
        Id<User> trainerId,
        Id<User> traineeId,
        ReportTemplate template,
        Id<CoachingEpisode>? sourceEpisodeId)
        => new()
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            TemplateId = template.Id,
            Template = template,
            Status = ReportRequestStatus.Pending,
            SourceCoachingEpisodeId = sourceEpisodeId
        };

    private static string FeedbackUrl(FeedbackScenario scenario)
        => $"/api/trainer/trainees/{scenario.Trainee.Id}/report-submissions/{scenario.SubmissionId}/feedback";

    private sealed record FeedbackScenario(
        User SourceTrainer,
        User ReplacementTrainer,
        User Trainee,
        Id<ReportSubmission> SubmissionId,
        Id<RecurringReportAssignment> AssignmentId,
        DateTimeOffset OriginalNextEligibleAt);

    private sealed record LegacySubmissionScenario(
        User Trainer,
        User Trainee,
        Id<ReportTemplate> TemplateId,
        Id<ReportRequest> LegacyRequestId);

    private sealed class GatedSaveUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public TaskCompletionSource<bool> SaveReached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseSave { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveReached.TrySetResult(true);
            await ReleaseSave.Task.WaitAsync(GateTimeout, cancellationToken);
            return await inner.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class GatedRelationshipAccessPersistence(
        IReportingRelationshipAccessPersistence inner) : IReportingRelationshipAccessPersistence
    {
        public TaskCompletionSource<bool> RevisionCheckStaged { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ReportingRelationshipAccessFact> GetAccessAsync(
            Id<AccountReference> trainerId,
            Id<AccountReference> traineeId,
            CancellationToken cancellationToken = default)
            => inner.GetAccessAsync(trainerId, traineeId, cancellationToken);

        public async Task<bool> StageActiveEpisodeRevisionCheckAsync(
            ReportingActiveRelationshipFact activeEpisode,
            CancellationToken cancellationToken = default)
        {
            var staged = await inner.StageActiveEpisodeRevisionCheckAsync(activeEpisode, cancellationToken);
            RevisionCheckStaged.TrySetResult(true);
            await Release.Task.WaitAsync(GateTimeout, cancellationToken);
            return staged;
        }

        public bool IsActiveEpisodeRevisionConflict(Exception exception)
            => inner.IsActiveEpisodeRevisionConflict(exception);
    }
}
