using System.Net;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Coaching.Relationships.UnlinkTrainee;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Authorization;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Notifications;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
public sealed partial class RecurringReportReminderIntegrationTests : PostgreSqlRecurringReportHttpTestBase
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task DueReminder_DeliveryAndReplay_ShowExactlyOneNotification(bool upcoming)
    {
        var scenario = await SeedScenarioAsync(upcoming);
        var envelopeId = await ProduceAsync();
        await DeliverAsync(envelopeId);
        await AssertVisibleCountAsync(scenario.TraineeId, 1);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var envelope = await database.CommandEnvelopes.SingleAsync(item => item.Id == envelopeId);
            envelope.Status = ActionExecutionStatus.Pending;
            envelope.CompletedAt = null;
            envelope.ProcessingStartedAtUtc = null;
            envelope.NextAttemptAt = null;
            await database.SaveChangesAsync();
        }

        await DeliverAsync(envelopeId);
        await AssertVisibleCountAsync(scenario.TraineeId, 1);
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var notifications = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>().InAppNotifications;
        (await notifications.CountAsync(item => item.RecipientId == scenario.TraineeId)).Should().Be(1);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ReminderSignalR_IsFencedButBridgeIsOutsideTransaction(bool upcoming)
    {
        var scenario = await SeedScenarioAsync(upcoming);
        await using var seedScope = Factory.Services.CreateAsyncScope();
        var assignment = await seedScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .RecurringReportAssignments.AsNoTracking().SingleAsync(item => item.TraineeId == scenario.TraineeId);
        var key = upcoming
            ? $"report-recurring-reminder:upcoming:{assignment.Id}:{assignment.ScheduleRevision}"
            : $"report-recurring-reminder:unanswered:{assignment.CurrentReportRequestId}";
        DeliveryTransactionProbe? probe = null;
        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInAppNotificationPushPublisher>();
            services.RemoveAll<INotificationEventBridge>();
            services.AddScoped(provider => probe = new DeliveryTransactionProbe(provider.GetRequiredService<AppDbContext>()));
            services.AddScoped<IInAppNotificationPushPublisher>(provider => provider.GetRequiredService<DeliveryTransactionProbe>());
            services.AddScoped<INotificationEventBridge>(provider => provider.GetRequiredService<DeliveryTransactionProbe>());
        }));
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IInAppNotificationService>().CreateAsync(
            new CreateInAppNotificationInput(scenario.TraineeId, scenario.TrainerId, key, false,
                "Reminder", "/reports", InAppNotificationTypes.ReportRequestReceived));

        result.IsSuccess.Should().BeTrue();
        probe.Should().NotBeNull();
        probe!.PublisherTransaction.Should().BeTrue();
        probe.BridgeTransaction.Should().BeFalse();
    }

    private sealed class DeliveryTransactionProbe(AppDbContext database) : IInAppNotificationPushPublisher, INotificationEventBridge
    {
        public bool? PublisherTransaction { get; private set; }
        public bool? BridgeTransaction { get; private set; }

        public Task PushAsync(InAppNotificationResult notification, CancellationToken ct = default)
        {
            PublisherTransaction = database.Database.CurrentTransaction != null;
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(EnqueueNotificationEventInput input, CancellationToken cancellationToken = default)
        {
            BridgeTransaction = database.Database.CurrentTransaction != null;
            return Task.CompletedTask;
        }
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task DueReminder_DeliveredAfterUnlink_IsNotEmitted(bool upcoming, bool trainerInitiated)
    {
        var scenario = await SeedScenarioAsync(upcoming);
        var envelopeId = await ProduceAsync();
        SetAuthorizationHeader(trainerInitiated ? scenario.TrainerId : scenario.TraineeId);
        var unlinkUrl = trainerInitiated
            ? $"/api/trainer/trainees/{scenario.TraineeId}/unlink"
            : "/api/trainee/trainer/detach";
        using var response = await Client.PostAsync(unlinkUrl, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await DeliverAsync(envelopeId);
        await AssertVisibleCountAsync(scenario.TraineeId, 0);
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.InAppNotifications.CountAsync(item => item.RecipientId == scenario.TraineeId)).Should().Be(0);
        (await database.CommandEnvelopes.SingleAsync(item => item.Id == envelopeId)).Status
            .Should().Be(ActionExecutionStatus.Completed);
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task ReminderAndUnlink_OverlappingTransactions_RespectCommitOrder(bool upcoming, bool unlinkFirst)
    {
        var scenario = await SeedScenarioAsync(upcoming);
        await using var seedScope = Factory.Services.CreateAsyncScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await seedDb.RecurringReportAssignments.AsNoTracking()
            .SingleAsync(item => item.TraineeId == scenario.TraineeId);
        var deliveryKey = upcoming
            ? $"report-recurring-reminder:upcoming:{assignment.Id}:{assignment.ScheduleRevision}"
            : $"report-recurring-reminder:unanswered:{assignment.CurrentReportRequestId}";
        var gate = new RaceGate();
        using var raceFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<INotificationSourceFactPort>();
            services.AddScoped<INotificationSourceFactPort>(provider =>
                new GatedSourceFacts(new NotificationSourceFactProvider(provider.GetRequiredService<AppDbContext>()), gate));
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(provider =>
            {
                var database = provider.GetRequiredService<AppDbContext>();
                return new GatedUnlinkUnitOfWork(new EfUnitOfWork(database), database, gate);
            });
        }));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        await using var reminderScope = raceFactory.Services.CreateAsyncScope();
        await using var unlinkScope = raceFactory.Services.CreateAsyncScope();
        var unlinkWork = unlinkScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await using var unlinkTransaction = await unlinkWork.BeginTransactionAsync(token);
        var reminder = reminderScope.ServiceProvider.GetRequiredService<IInAppNotificationService>().CreateAsync(
            new CreateInAppNotificationInput(scenario.TraineeId, scenario.TrainerId, deliveryKey, false,
                "Reminder", "/reports", InAppNotificationTypes.ReportRequestReceived), token);
        try
        {
            (await gate.ReminderReached.Task.WaitAsync(TimeSpan.FromSeconds(10), token)).Should().BeTrue();
            var unlink = unlinkScope.ServiceProvider.GetRequiredService<IUnlinkTraineeUseCase>()
                .ExecuteAsync(new UnlinkTraineeCommand(scenario.TrainerId, scenario.TraineeId), token);
            await gate.UnlinkReached.Task.WaitAsync(TimeSpan.FromSeconds(10), token);

            if (unlinkFirst)
            {
                gate.ReleaseUnlink.TrySetResult();
                (await unlink.WaitAsync(TimeSpan.FromSeconds(10), token)).IsSuccess.Should().BeTrue();
                await unlinkTransaction.CommitAsync(token);
                gate.ReleaseReminder.TrySetResult();
                var conflict = async () => await reminder.WaitAsync(TimeSpan.FromSeconds(10), token);
                await conflict.Should().ThrowAsync<DbUpdateConcurrencyException>();
            }
            else
            {
                gate.ReleaseReminder.TrySetResult();
                (await reminder.WaitAsync(TimeSpan.FromSeconds(10), token)).IsSuccess.Should().BeTrue();
                gate.ReleaseUnlink.TrySetResult();
                (await unlink.WaitAsync(TimeSpan.FromSeconds(10), token)).IsSuccess.Should().BeTrue();
                await unlinkTransaction.CommitAsync(token);
            }
        }
        finally
        {
            gate.ReleaseReminder.TrySetResult();
            gate.ReleaseUnlink.TrySetResult();
        }

        await using var checkScope = Factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.InAppNotifications.CountAsync(item => item.RecipientId == scenario.TraineeId && item.DeliveryKey == deliveryKey, token))
            .Should().Be(unlinkFirst ? 0 : 1);
        (await db.TrainerTraineeLinks.AnyAsync(item => item.TraineeId == scenario.TraineeId, token)).Should().BeFalse();
        var episode = await db.CoachingEpisodes.SingleAsync(item => item.TraineeId == scenario.TraineeId, token);
        episode.EndedAt.Should().NotBeNull();
        episode.AuthorizationRevision.Should().Be(1);
    }

    private async Task<(Id<User> TrainerId, Id<User> TraineeId)> SeedScenarioAsync(bool upcoming)
    {
        var trainer = await SeedUserAsync("reminder-trainer", "reminder-trainer@example.test");
        var trainee = await SeedUserAsync("reminder-trainee", "reminder-trainee@example.test");
        var now = DateTimeOffset.UtcNow;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var episode = new CoachingEpisode
        {
            Id = Id<CoachingEpisode>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id,
            StartedAt = now.AddDays(-5), StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation
        };
        var template = new ReportTemplate { Id = Id<ReportTemplate>.New(), TrainerId = trainer.Id, Name = "Reminder" };
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id,
            TemplateId = template.Id, SourceCoachingEpisodeId = episode.Id,
            IntervalValue = 1, IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = upcoming ? now.AddHours(12) : now.AddDays(-4),
            NextEligibleAt = upcoming ? now.AddHours(12) : null
        };
        if (!upcoming)
        {
            var request = new ReportRequest
            {
                Id = Id<ReportRequest>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id,
                TemplateId = template.Id, SourceCoachingEpisodeId = episode.Id,
                Status = ReportRequestStatus.Pending, CreatedAt = now.AddHours(-49)
            };
            assignment.CurrentReportRequestId = request.Id;
            db.ReportRequests.Add(request);
        }
        db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
        db.CoachingEpisodes.Add(episode);
        db.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = trainee.Id, EpisodeId = episode.Id
        });
        db.ReportTemplates.Add(template);
        db.RecurringReportAssignments.Add(assignment);
        await db.SaveChangesAsync();
        return (trainer.Id, trainee.Id);
    }

    private async Task<Id<CommandEnvelope>> ProduceAsync()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRecurringReportAssignmentService>().ProcessDueAssignmentsAsync();
        }
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICommittedIntentDispatchJob>().ExecuteAsync();
        }
        await using var readScope = Factory.Services.CreateAsyncScope();
        var envelopes = await readScope.ServiceProvider.GetRequiredService<AppDbContext>().CommandEnvelopes.AsNoTracking().ToArrayAsync();
        return envelopes.Should().ContainSingle().Subject.Id;
    }

    private async Task DeliverAsync(Id<CommandEnvelope> envelopeId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IActionMessageJob>().ExecuteAsync(envelopeId.ToString());
    }

    private async Task AssertVisibleCountAsync(Id<User> traineeId, int count)
    {
        SetAuthorizationHeader(traineeId);
        using var response = await Client.GetAsync($"/api/{traineeId}/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("items").GetArrayLength().Should().Be(count);
    }

    private sealed class RaceGate
    {
        public TaskCompletionSource<bool> ReminderReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource UnlinkReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseReminder { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseUnlink { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedSourceFacts(INotificationSourceFactPort inner, RaceGate gate) : INotificationSourceFactPort
    {
        public async Task<bool> StageReminderFenceAsync(NotificationSourceReference reference, CancellationToken cancellationToken = default)
        {
            var staged = await inner.StageReminderFenceAsync(reference, cancellationToken);
            gate.ReminderReached.TrySetResult(staged);
            await gate.ReleaseReminder.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return staged;
        }

        public Task<IReadOnlyDictionary<Id<InAppNotification>, NotificationSourceFacts>> GetFactsAsync(
            IReadOnlyCollection<NotificationSourceReference> references, CancellationToken cancellationToken = default)
            => inner.GetFactsAsync(references, cancellationToken);
    }

    private sealed class GatedUnlinkUnitOfWork(IUnitOfWork inner, AppDbContext database, RaceGate gate) : IUnitOfWork
    {
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (database.ChangeTracker.Entries<TrainerTraineeLink>().Any(entry => entry.State == EntityState.Deleted))
            {
                gate.UnlinkReached.TrySetResult();
                await gate.ReleaseUnlink.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return await inner.SaveChangesAsync(cancellationToken);
        }
    }
}
