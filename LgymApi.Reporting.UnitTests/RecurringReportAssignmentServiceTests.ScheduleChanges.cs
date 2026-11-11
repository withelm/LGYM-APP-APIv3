using FluentAssertions;
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

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.ScheduleRevision.Should().Be(2);
        second.Value.EffectiveNextRecurringRequest.ScheduledAt.Should().Be(secondAt);
        database.RecurringReportScheduleChanges.Should().HaveCount(2);
        database.RecurringReportScheduleChanges.Count(change => change.Status == RecurringReportScheduleChangeStatus.Active).Should().Be(1);
        database.RecurringReportScheduleChanges.Count(change => change.Status == RecurringReportScheduleChangeStatus.Superseded).Should().Be(1);
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

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingConflictError>();
        database.RecurringReportScheduleChanges.Should().BeEmpty();
        database.RecurringReportAssignments.Single().ScheduleRevision.Should().Be(3);
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

        result.IsSuccess.Should().BeTrue();
        result.Value.EffectiveNextRecurringRequest.ScheduledAt.Should().Be(scheduledAt);
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

        proposed.IsSuccess.Should().BeTrue();
        proposed.Value.NextEligibleAt.Should().Be(originalAt);
        proposed.Value.EffectiveNextRecurringRequest.ScheduledAt.Should().Be(originalAt);
        accepted.IsSuccess.Should().BeTrue();
        accepted.Value.ScheduleRevision.Should().Be(1);
        accepted.Value.EffectiveNextRecurringRequest.ScheduledAt.Should().Be(proposedAt);
        duplicate.IsFailure.Should().BeTrue();
        duplicate.Error.Should().BeOfType<ReportingConflictError>();
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

        rejected.IsSuccess.Should().BeTrue();
        rejected.Value.ScheduleRevision.Should().Be(0);
        rejected.Value.EffectiveNextRecurringRequest.ScheduledAt.Should().Be(originalAt);
        replacement.IsSuccess.Should().BeTrue();
        replacement.Value.PendingScheduleProposal.Should().NotBeNull();
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

        proposed.Value.PendingScheduleProposal.Should().NotBeNull();
        paused.IsSuccess.Should().BeTrue();
        paused.Value.PendingScheduleProposal.Should().BeNull();
        paused.Value.ScheduleRevision.Should().Be(1);
        resumed.IsSuccess.Should().BeTrue();
        resumed.Value.PendingScheduleProposal.Should().BeNull();
        resumed.Value.ScheduleRevision.Should().Be(2);
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

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
        harness.UnitOfWork.SaveCalls.Should().Be(1);
        harness.Transaction.RollbackCalls.Should().Be(1);
        harness.Transaction.CommitCalls.Should().Be(0);
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
