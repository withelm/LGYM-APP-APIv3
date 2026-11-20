using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using NSubstitute;

namespace LgymApi.UnitTests;

public sealed partial class RecurringReportAssignmentServiceTests
{
    [Test]
    public async Task SetNextOverrideAsync_RepeatedRevisionSupersedesPriorOverrideAndKeepsAuditHistory()
    {
        await using var database = CreateDbContext("schedule-repeated-override");
        var scenario = await SeedScheduleScenarioAsync(database);
        var service = CreateService(database, scenario.Trainer.Id, scenario.Trainee.Id, ownsTrainee: true);
        var firstAt = DateTimeOffset.UtcNow.AddDays(2);
        var secondAt = firstAt.AddDays(1);

        var first = await service.SetNextOverrideAsync(
            ReportingTestData.Account(scenario.Trainer.Id, true),
            ReportingTestData.AccountId(scenario.Trainee.Id),
            scenario.Assignment.Id,
            new SetNextRecurringReportOverrideCommand { ScheduledAt = firstAt, ExpectedScheduleRevision = 0 });
        var second = await service.SetNextOverrideAsync(
            ReportingTestData.Account(scenario.Trainer.Id, true),
            ReportingTestData.AccountId(scenario.Trainee.Id),
            scenario.Assignment.Id,
            new SetNextRecurringReportOverrideCommand { ScheduledAt = secondAt, ExpectedScheduleRevision = 1 });

        Assert.That(first.IsSuccess, Is.True);
        Assert.That(second.IsSuccess, Is.True);
        Assert.That(second.Value.ScheduleRevision, Is.EqualTo(2));
        Assert.That(second.Value.EffectiveNextRecurringRequest.ScheduledAt, Is.EqualTo(secondAt));
        Assert.That(database.RecurringReportScheduleChanges, Has.Exactly(2).Items);
        Assert.That(database.RecurringReportScheduleChanges.Count(change => change.Status == RecurringReportScheduleChangeStatus.Active), Is.EqualTo(1));
        Assert.That(database.RecurringReportScheduleChanges.Count(change => change.Status == RecurringReportScheduleChangeStatus.Superseded), Is.EqualTo(1));
    }

    [Test]
    public async Task SetNextOverrideAsync_WithStaleExpectedRevision_ReturnsConflictWithoutHistoryWrite()
    {
        await using var database = CreateDbContext("schedule-stale-override");
        var scenario = await SeedScheduleScenarioAsync(database);
        scenario.Assignment.ScheduleRevision = 3;
        await database.SaveChangesAsync();
        var service = CreateService(database, scenario.Trainer.Id, scenario.Trainee.Id, ownsTrainee: true);

        var result = await service.SetNextOverrideAsync(
            ReportingTestData.Account(scenario.Trainer.Id, true),
            ReportingTestData.AccountId(scenario.Trainee.Id),
            scenario.Assignment.Id,
            new SetNextRecurringReportOverrideCommand
            {
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(2),
                ExpectedScheduleRevision = 2
            });

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ReportingConflictError>());
        Assert.That(database.RecurringReportScheduleChanges, Is.Empty);
        Assert.That(database.RecurringReportAssignments.Single().ScheduleRevision, Is.EqualTo(3));
    }

    [Test]
    public async Task SetNextOverrideAsync_BeforeAssignmentStarts_AllowsFutureOccurrence()
    {
        await using var database = CreateDbContext("schedule-pre-start");
        var startsAt = DateTimeOffset.UtcNow.AddDays(3);
        var scenario = await SeedScheduleScenarioAsync(database, startsAt);
        var service = CreateService(database, scenario.Trainer.Id, scenario.Trainee.Id, ownsTrainee: true);
        var scheduledAt = startsAt.AddHours(4);

        var result = await service.SetNextOverrideAsync(
            ReportingTestData.Account(scenario.Trainer.Id, true),
            ReportingTestData.AccountId(scenario.Trainee.Id),
            scenario.Assignment.Id,
            new SetNextRecurringReportOverrideCommand { ScheduledAt = scheduledAt, ExpectedScheduleRevision = 0 });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.EffectiveNextRecurringRequest.ScheduledAt, Is.EqualTo(scheduledAt));
    }

    [Test]
    public async Task ProposeAndAcceptScheduleAsync_LeavesScheduleUnchangedUntilTrainerAccepts()
    {
        await using var database = CreateDbContext("schedule-proposal-accept");
        var scenario = await SeedScheduleScenarioAsync(database);
        var service = CreateService(database, scenario.Trainer.Id, scenario.Trainee.Id, ownsTrainee: true);
        var originalAt = scenario.Assignment.NextEligibleAt;
        var proposedAt = DateTimeOffset.UtcNow.AddDays(4);

        var proposed = await service.ProposeNextScheduleAsync(
            ReportingTestData.Account(scenario.Trainee.Id),
            scenario.Assignment.Id,
            new ProposeNextRecurringReportScheduleCommand
            {
                ScheduledAt = proposedAt,
                Reason = "Travel",
                ExpectedScheduleRevision = 0
            });
        var accepted = await service.ResolveScheduleProposalAsync(
            ReportingTestData.Account(scenario.Trainer.Id, true),
            ReportingTestData.AccountId(scenario.Trainee.Id),
            scenario.Assignment.Id,
            proposed.Value.PendingScheduleProposal!.Id,
            true);
        var duplicate = await service.ResolveScheduleProposalAsync(
            ReportingTestData.Account(scenario.Trainer.Id, true),
            ReportingTestData.AccountId(scenario.Trainee.Id),
            scenario.Assignment.Id,
            proposed.Value.PendingScheduleProposal.Id,
            true);

        Assert.That(proposed.IsSuccess, Is.True);
        Assert.That(proposed.Value.NextEligibleAt, Is.EqualTo(originalAt));
        Assert.That(proposed.Value.EffectiveNextRecurringRequest.ScheduledAt, Is.EqualTo(originalAt));
        Assert.That(accepted.IsSuccess, Is.True);
        Assert.That(accepted.Value.ScheduleRevision, Is.EqualTo(1));
        Assert.That(accepted.Value.EffectiveNextRecurringRequest.ScheduledAt, Is.EqualTo(proposedAt));
        Assert.That(duplicate.IsFailure, Is.True);
        Assert.That(duplicate.Error, Is.TypeOf<ReportingConflictError>());
    }

    [Test]
    public async Task RejectScheduleProposalAsync_DoesNotChangeScheduleAndAllowsReplacementProposal()
    {
        await using var database = CreateDbContext("schedule-proposal-reject");
        var scenario = await SeedScheduleScenarioAsync(database);
        var service = CreateService(database, scenario.Trainer.Id, scenario.Trainee.Id, ownsTrainee: true);
        var originalAt = scenario.Assignment.NextEligibleAt;
        var command = new ProposeNextRecurringReportScheduleCommand
        {
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(5),
            ExpectedScheduleRevision = 0
        };
        var proposed = await service.ProposeNextScheduleAsync(
            ReportingTestData.Account(scenario.Trainee.Id), scenario.Assignment.Id, command);

        var rejected = await service.ResolveScheduleProposalAsync(
            ReportingTestData.Account(scenario.Trainer.Id, true),
            ReportingTestData.AccountId(scenario.Trainee.Id),
            scenario.Assignment.Id,
            proposed.Value.PendingScheduleProposal!.Id,
            false);
        var replacement = await service.ProposeNextScheduleAsync(
            ReportingTestData.Account(scenario.Trainee.Id),
            scenario.Assignment.Id,
            new ProposeNextRecurringReportScheduleCommand
            {
                ScheduledAt = command.ScheduledAt.AddDays(1),
                ExpectedScheduleRevision = command.ExpectedScheduleRevision
            });

        Assert.That(rejected.IsSuccess, Is.True);
        Assert.That(rejected.Value.ScheduleRevision, Is.EqualTo(0));
        Assert.That(rejected.Value.EffectiveNextRecurringRequest.ScheduledAt, Is.EqualTo(originalAt));
        Assert.That(replacement.IsSuccess, Is.True);
        Assert.That(replacement.Value.PendingScheduleProposal, Is.Not.Null);
    }

    [Test]
    public async Task PauseAndResumeAsync_InvalidatesPendingScheduleStateWithoutResurrection()
    {
        await using var database = CreateDbContext("schedule-pause-resume");
        var scenario = await SeedScheduleScenarioAsync(database);
        var service = CreateService(database, scenario.Trainer.Id, scenario.Trainee.Id, ownsTrainee: true);
        var proposed = await service.ProposeNextScheduleAsync(
            ReportingTestData.Account(scenario.Trainee.Id),
            scenario.Assignment.Id,
            new ProposeNextRecurringReportScheduleCommand
            {
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(3),
                ExpectedScheduleRevision = 0
            });

        var paused = await service.PauseAsync(
            ReportingTestData.Account(scenario.Trainer.Id, true),
            ReportingTestData.AccountId(scenario.Trainee.Id),
            scenario.Assignment.Id);
        var resumed = await service.ResumeAsync(
            ReportingTestData.Account(scenario.Trainer.Id, true),
            ReportingTestData.AccountId(scenario.Trainee.Id),
            scenario.Assignment.Id);

        Assert.That(proposed.Value.PendingScheduleProposal, Is.Not.Null);
        Assert.That(paused.IsSuccess, Is.True);
        Assert.That(paused.Value.PendingScheduleProposal, Is.Null);
        Assert.That(paused.Value.ScheduleRevision, Is.EqualTo(1));
        Assert.That(resumed.IsSuccess, Is.True);
        Assert.That(resumed.Value.PendingScheduleProposal, Is.Null);
        Assert.That(resumed.Value.ScheduleRevision, Is.EqualTo(2));
    }

    [Test]
    public async Task SetNextOverrideAsync_WhenEpisodeRevisionChangesDuringSave_ReturnsNotFoundAndRollsBack()
    {
        var assignment = CreateProcessingAssignment() with
        {
            CurrentReportRequestId = null,
            CurrentReportRequest = null,
            NextEligibleAt = DateTimeOffset.UtcNow.AddDays(1),
            ScheduleRevision = 0
        };
        var harness = CreateRequestNowHarness(assignment);
        var concurrencyException = new InvalidOperationException("episode revision changed");
        harness.UnitOfWork.SaveException = concurrencyException;
        harness.RelationshipPersistence.IsActiveEpisodeRevisionConflict(concurrencyException).Returns(true);
        harness.AssignmentPersistence
            .AddScheduleChangeAsync(Arg.Any<NewRecurringReportScheduleChangePersistenceModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await harness.Service.SetNextOverrideAsync(
            RequestNowAccount(assignment.TrainerId),
            assignment.TraineeId,
            assignment.Id,
            new SetNextRecurringReportOverrideCommand
            {
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(2),
                ExpectedScheduleRevision = 0
            });

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.TypeOf<ReportingNotFoundError>());
        Assert.That(harness.UnitOfWork.SaveCalls, Is.EqualTo(1));
        Assert.That(harness.Transaction.RollbackCalls, Is.EqualTo(1));
        Assert.That(harness.Transaction.CommitCalls, Is.EqualTo(0));
    }

    private static async Task<ScheduleScenario> SeedScheduleScenarioAsync(
        LgymApi.Infrastructure.Data.AppDbContext database,
        DateTimeOffset? startsAt = null)
    {
        var trainer = CreateUser();
        var trainee = CreateUser(Id<User>.New(), "Trainee", $"{Id<User>.New():N}@example.com");
        var template = CreateTemplate(trainer.Id);
        var assignment = CreateAssignment(trainer.Id, trainee.Id, template.Id);
        assignment.StartsAt = startsAt ?? DateTimeOffset.UtcNow.AddDays(-2);
        assignment.NextEligibleAt = assignment.StartsAt.AddDays(7);
        database.Users.AddRange(trainer, trainee);
        database.ReportTemplates.Add(template);
        database.RecurringReportAssignments.Add(assignment);
        await database.SaveChangesAsync();
        return new ScheduleScenario(trainer, trainee, assignment);
    }

    private sealed record ScheduleScenario(User Trainer, User Trainee, RecurringReportAssignment Assignment);
}
