using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Repositories;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlRecurringReportRequestConcurrencyTests : PostgreSqlIntegrationTestBase
{
    private const string CanonicalCommandId =
        "LgymApi.BackgroundWorker.Common.Commands.ReportRequestCreatedInAppNotificationCommand";
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    [Test]
    public async Task RequestNow_ConcurrentManualCalls_FirstManualWins()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var scenario = await SeedScenarioAsync(DateTimeOffset.UtcNow.AddMonths(1), cancellationToken);
        await using var firstScope = Factory.Services.CreateAsyncScope();
        await using var secondScope = Factory.Services.CreateAsyncScope();
        var first = CreateRaceService(firstScope.ServiceProvider);
        var second = CreateRaceService(secondScope.ServiceProvider);

        var firstTask = first.Service.RequestNowAsync(
            scenario.Trainer,
            scenario.TraineeId,
            scenario.AssignmentId,
            cancellationToken);
        var secondTask = second.Service.RequestNowAsync(
            scenario.Trainer,
            scenario.TraineeId,
            scenario.AssignmentId,
            cancellationToken);

        await WaitUntilBothReachedAsync(first.Gate, second.Gate, cancellationToken);
        Assert.That(first.Gate.ReleaseToLock.TrySetResult(true), Is.True);
        await first.Gate.LockAcquired.Task.WaitAsync(GateTimeout, cancellationToken);
        Assert.That(second.Gate.ReleaseToLock.TrySetResult(true), Is.True);
        var results = await Task.WhenAll(firstTask, secondTask).WaitAsync(GateTimeout, cancellationToken);

        Assert.That(results[0].IsSuccess, Is.True);
        Assert.That(results[1].IsFailure, Is.True);
        Assert.That(results[1].Error, Is.TypeOf<ReportingConflictError>());
        Assert.That(results[1].Error.Message, Is.EqualTo(Messages.RecurringReportRequestInProgress));
        Assert.That(first.UnitOfWork.SaveChangesCalls, Is.EqualTo(1));
        Assert.That(first.UnitOfWork.CommitCalls, Is.EqualTo(1));
        Assert.That(second.UnitOfWork.SaveChangesCalls, Is.EqualTo(0));
        Assert.That(second.UnitOfWork.RollbackCalls, Is.EqualTo(1));
        await AssertSingleCreationAsync(scenario.AssignmentId, cancellationToken);
        WriteGateEvidence("manual-a", first.Gate, second.Gate, "manual-success/manual-conflict");
    }

    [Test]
    public async Task RequestNow_ConcurrentWithAutomatic_ManualWins()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var scenario = await SeedScenarioAsync(DateTimeOffset.UtcNow.AddDays(-1), cancellationToken);
        await using var manualScope = Factory.Services.CreateAsyncScope();
        await using var automaticScope = Factory.Services.CreateAsyncScope();
        var manual = CreateRaceService(manualScope.ServiceProvider);
        var automatic = CreateRaceService(automaticScope.ServiceProvider);

        var manualTask = manual.Service.RequestNowAsync(
            scenario.Trainer,
            scenario.TraineeId,
            scenario.AssignmentId,
            cancellationToken);
        var automaticTask = automatic.Service.ProcessDueAssignmentsAsync(cancellationToken);

        await WaitUntilBothReachedAsync(manual.Gate, automatic.Gate, cancellationToken);
        Assert.That(manual.Gate.ReleaseToLock.TrySetResult(true), Is.True);
        await manual.Gate.LockAcquired.Task.WaitAsync(GateTimeout, cancellationToken);
        Assert.That(automatic.Gate.ReleaseToLock.TrySetResult(true), Is.True);
        await Task.WhenAll(manualTask, automaticTask).WaitAsync(GateTimeout, cancellationToken);

        var manualResult = await manualTask;
        Assert.That(manualResult.IsSuccess, Is.True);
        Assert.That(manual.UnitOfWork.SaveChangesCalls, Is.EqualTo(1));
        Assert.That(manual.UnitOfWork.CommitCalls, Is.EqualTo(1));
        Assert.That(automatic.UnitOfWork.SaveChangesCalls, Is.EqualTo(0));
        Assert.That(automatic.UnitOfWork.RollbackCalls, Is.EqualTo(1));
        await AssertSingleCreationAsync(scenario.AssignmentId, cancellationToken);
        WriteGateEvidence("manual", manual.Gate, automatic.Gate, "manual-success/automatic-skip");
    }

    [Test]
    public async Task RequestNow_ConcurrentWithAutomatic_AutomaticWins()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var scenario = await SeedScenarioAsync(DateTimeOffset.UtcNow.AddDays(-1), cancellationToken);
        await using var manualScope = Factory.Services.CreateAsyncScope();
        await using var automaticScope = Factory.Services.CreateAsyncScope();
        var manual = CreateRaceService(manualScope.ServiceProvider);
        var automatic = CreateRaceService(automaticScope.ServiceProvider);

        var manualTask = manual.Service.RequestNowAsync(
            scenario.Trainer,
            scenario.TraineeId,
            scenario.AssignmentId,
            cancellationToken);
        var automaticTask = automatic.Service.ProcessDueAssignmentsAsync(cancellationToken);

        await WaitUntilBothReachedAsync(manual.Gate, automatic.Gate, cancellationToken);
        Assert.That(automatic.Gate.ReleaseToLock.TrySetResult(true), Is.True);
        await automatic.Gate.LockAcquired.Task.WaitAsync(GateTimeout, cancellationToken);
        Assert.That(manual.Gate.ReleaseToLock.TrySetResult(true), Is.True);
        await Task.WhenAll(manualTask, automaticTask).WaitAsync(GateTimeout, cancellationToken);

        var manualResult = await manualTask;
        Assert.That(manualResult.IsFailure, Is.True);
        Assert.That(manualResult.Error, Is.TypeOf<ReportingConflictError>());
        Assert.That(manualResult.Error.Message, Is.EqualTo(Messages.RecurringReportRequestInProgress));
        Assert.That(automatic.UnitOfWork.SaveChangesCalls, Is.EqualTo(1));
        Assert.That(automatic.UnitOfWork.CommitCalls, Is.EqualTo(1));
        Assert.That(manual.UnitOfWork.SaveChangesCalls, Is.EqualTo(0));
        Assert.That(manual.UnitOfWork.RollbackCalls, Is.EqualTo(1));
        await AssertSingleCreationAsync(scenario.AssignmentId, cancellationToken);
        WriteGateEvidence("automatic", automatic.Gate, manual.Gate, "automatic-created/manual-conflict");
    }

    private async Task<RaceScenario> SeedScenarioAsync(
        DateTimeOffset nextEligibleAt,
        CancellationToken cancellationToken)
    {
        var trainer = await SeedUserAsync(
            $"request-now-trainer-{Id<User>.New():N}",
            $"request-now-trainer-{Id<User>.New():N}@example.com");
        var trainee = await SeedUserAsync(
            $"request-now-trainee-{Id<User>.New():N}",
            $"request-now-trainee-{Id<User>.New():N}@example.com");
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Concurrency template"
        };
        var episode = new CoachingEpisode
        {
            Id = Id<CoachingEpisode>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-10),
            StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
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
            StartsAt = DateTimeOffset.UtcNow.AddDays(-10),
            IsActive = true,
            NextEligibleAt = nextEligibleAt,
            SourceCoachingEpisodeId = episode.Id
        };
        database.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            EpisodeId = episode.Id
        });
        database.AddRange(template, assignment, episode);
        await database.SaveChangesAsync(cancellationToken);
        return new RaceScenario(
            new AuthenticatedAccountContext(
                trainer.Id.Rebind<AccountReference>(),
                null,
                [AuthConstants.Roles.Trainer],
                [],
                false,
                false),
            trainee.Id.Rebind<AccountReference>(),
            assignment.Id);
    }

    private static RaceService CreateRaceService(IServiceProvider serviceProvider)
    {
        var gate = new AssignmentLockGate();
        var assignmentPersistence = new GatedAssignmentPersistence(
            serviceProvider.GetRequiredService<IRecurringReportAssignmentPersistence>(),
            gate);
        var unitOfWork = new ObservedUnitOfWork(serviceProvider.GetRequiredService<IUnitOfWork>());
        var service = new RecurringReportAssignmentService(
            serviceProvider.GetRequiredService<IReportTemplatePersistence>(),
            serviceProvider.GetRequiredService<IReportRequestSubmissionPersistence>(),
            assignmentPersistence,
            serviceProvider.GetRequiredService<IReportingRelationshipAccessPersistence>(),
            serviceProvider.GetRequiredService<IMapper>(),
            serviceProvider.GetRequiredService<ICommandOutboxWriter>(),
            unitOfWork,
            NullLogger<RecurringReportAssignmentService>.Instance);
        return new RaceService(service, gate, unitOfWork);
    }

    private static async Task WaitUntilBothReachedAsync(
        AssignmentLockGate first,
        AssignmentLockGate second,
        CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            first.ReachedLock.Task,
            second.ReachedLock.Task).WaitAsync(GateTimeout, cancellationToken);
        Assert.That(first.ReachedCount, Is.EqualTo(1));
        Assert.That(second.ReachedCount, Is.EqualTo(1));
    }

    private async Task AssertSingleCreationAsync(
        Id<RecurringReportAssignment> assignmentId,
        CancellationToken cancellationToken)
    {
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var database = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await database.RecurringReportAssignments
            .AsNoTracking()
            .SingleAsync(value => value.Id == assignmentId, cancellationToken);
        var requests = await database.ReportRequests
            .AsNoTracking()
            .Where(request => request.RecurringReportAssignmentId == assignmentId)
            .ToListAsync(cancellationToken);
        var envelopes = await database.CommandEnvelopes
            .AsNoTracking()
            .Where(envelope => envelope.CommandTypeFullName == CanonicalCommandId)
            .ToListAsync(cancellationToken);

        Assert.That(requests, Has.Exactly(1).Items);
        var request = requests[0];
        Assert.That(request.Status, Is.EqualTo(ReportRequestStatus.Pending));
        Assert.That(assignment.CurrentReportRequestId, Is.EqualTo(request.Id));
        Assert.That(assignment.LastRequestCreatedAt, Is.EqualTo(request.CreatedAt));
        Assert.That(assignment.NextEligibleAt, Is.Null);
        Assert.That(envelopes, Has.Exactly(1).Items);
        Assert.That(envelopes[0].PayloadJson, Does.Contain(request.Id.Value.ToString()));
    }

    private static void WriteGateEvidence(
        string winner,
        AssignmentLockGate winnerGate,
        AssignmentLockGate loserGate,
        string outcomes)
        => TestContext.Progress.WriteLine(
            $"winner={winner}; bothReached={winnerGate.ReachedCount == 1 && loserGate.ReachedCount == 1}; "
            + $"winnerLockAcquired={winnerGate.AcquiredCount == 1}; loserLockAcquired={loserGate.AcquiredCount == 1}; outcomes={outcomes}");

    private sealed record RaceScenario(
        AuthenticatedAccountContext Trainer,
        Id<AccountReference> TraineeId,
        Id<RecurringReportAssignment> AssignmentId);

    private sealed record RaceService(
        RecurringReportAssignmentService Service,
        AssignmentLockGate Gate,
        ObservedUnitOfWork UnitOfWork);

    private sealed class AssignmentLockGate
    {
        private int _reachedCount;
        private int _acquiredCount;

        public TaskCompletionSource<bool> ReachedLock { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseToLock { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> LockAcquired { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ReachedCount => _reachedCount;
        public int AcquiredCount => _acquiredCount;

        public void ReportReached()
        {
            Interlocked.Increment(ref _reachedCount);
            ReachedLock.TrySetResult(true);
        }

        public void ReportAcquired()
        {
            Interlocked.Increment(ref _acquiredCount);
            LockAcquired.TrySetResult(true);
        }
    }

    private sealed class GatedAssignmentPersistence(
        IRecurringReportAssignmentPersistence inner,
        AssignmentLockGate gate) : IRecurringReportAssignmentPersistence
    {
        public Task AddAsync(NewRecurringReportAssignmentPersistenceModel assignment, CancellationToken cancellationToken = default)
            => inner.AddAsync(assignment, cancellationToken);

        public Task<RecurringReportAssignmentPersistenceModel?> FindForTrainerAsync(Id<RecurringReportAssignment> assignmentId, Id<AccountReference> trainerId, Id<AccountReference> traineeId, CancellationToken cancellationToken = default)
            => inner.FindForTrainerAsync(assignmentId, trainerId, traineeId, cancellationToken);

        public Task<RecurringReportAssignmentPersistenceModel?> FindByIdAsync(Id<RecurringReportAssignment> assignmentId, CancellationToken cancellationToken = default)
            => inner.FindByIdAsync(assignmentId, cancellationToken);

        public async Task<RecurringReportAssignmentPersistenceModel?> FindByIdForUpdateAsync(Id<RecurringReportAssignment> assignmentId, CancellationToken cancellationToken = default)
        {
            gate.ReportReached();
            await gate.ReleaseToLock.Task.WaitAsync(GateTimeout, cancellationToken);
            var assignment = await inner.FindByIdForUpdateAsync(assignmentId, cancellationToken);
            gate.ReportAcquired();
            return assignment;
        }

        public Task<RecurringReportAssignmentPersistenceModel?> FindByCurrentRequestAsync(Id<ReportRequest> reportRequestId, CancellationToken cancellationToken = default)
            => inner.FindByCurrentRequestAsync(reportRequestId, cancellationToken);

        public Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListByCurrentRequestsAsync(IReadOnlyCollection<Id<ReportRequest>> reportRequestIds, CancellationToken cancellationToken = default)
            => inner.ListByCurrentRequestsAsync(reportRequestIds, cancellationToken);

        public Task<RecurringReportAssignmentPersistenceModel?> FindByCurrentRequestForUpdateAsync(Id<ReportRequest> reportRequestId, CancellationToken cancellationToken = default)
            => inner.FindByCurrentRequestForUpdateAsync(reportRequestId, cancellationToken);

        public Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListByTrainerAndTraineeAsync(Id<AccountReference> trainerId, Id<AccountReference> traineeId, CancellationToken cancellationToken = default)
            => inner.ListByTrainerAndTraineeAsync(trainerId, traineeId, cancellationToken);

        public Task<IReadOnlyList<RecurringReportAssignmentPersistenceModel>> ListDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
            => inner.ListDueAsync(now, cancellationToken);

        public Task UpdateAsync(Id<RecurringReportAssignment> assignmentId, RecurringReportAssignmentUpdatePersistenceModel update, CancellationToken cancellationToken = default)
            => inner.UpdateAsync(assignmentId, update, cancellationToken);
    }
}
