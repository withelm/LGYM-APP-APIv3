using System.Text.Json;
using LgymApi.Application.Coaching.Relationships.UnlinkTrainee;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace LgymApi.IntegrationTests;

public sealed partial class RecurringReportReminderIntegrationTests
{
    private sealed record DeliveryScenario(
        CreateInAppNotificationInput Input,
        Id<RecurringReportAssignment> AssignmentId,
        Id<ReportRequest>? RequestId,
        Id<PushNotificationMessage> MessageId);

    private async Task<DeliveryScenario> SeedDeliveryRaceAsync(bool upcoming, bool remotePush)
    {
        var scenario = await SeedScenarioAsync(upcoming);
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await database.RecurringReportAssignments.AsNoTracking()
            .SingleAsync(item => item.TraineeId == scenario.TraineeId);
        var key = upcoming
            ? $"report-recurring-reminder:upcoming:{assignment.Id}:{assignment.ScheduleRevision}"
            : $"report-recurring-reminder:unanswered:{assignment.CurrentReportRequestId}";
        var input = new CreateInAppNotificationInput(scenario.TraineeId, scenario.TrainerId, key, false,
            "Reminder", "/reports", InAppNotificationTypes.ReportRequestReceived);
        var messageId = Id<PushNotificationMessage>.New();
        if (remotePush)
        {
            var session = new UserSession
            {
                Id = Id<UserSession>.New(), UserId = scenario.TraineeId,
                Jti = Id<UserSession>.New().ToString(), ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1)
            };
            var installation = new PushInstallation
            {
                Id = Id<PushInstallation>.New(), UserId = scenario.TraineeId, SessionId = session.Id,
                InstallationId = Id<PushInstallation>.New().ToString(), FcmToken = Id<PushInstallation>.New().ToString(),
                Platform = "android", Environment = "testing", LastSeenAt = DateTimeOffset.UtcNow
            };
            var notification = new InAppNotification
            {
                Id = Id<InAppNotification>.New(), RecipientId = scenario.TraineeId,
                SenderUserId = scenario.TrainerId, DeliveryKey = key, Message = input.Message,
                RedirectUrl = input.RedirectUrl, Type = input.Type
            };
            var payload = new PushEventPayload(1, input.Type.Value, notification.Id.ToString(), null, notification.Id, input.RedirectUrl);
            database.UserSessions.Add(session);
            database.PushInstallations.Add(installation);
            database.InAppNotifications.Add(notification);
            database.PushNotificationMessages.Add(new PushNotificationMessage
            {
                Id = messageId, UserId = scenario.TraineeId, PushInstallationId = installation.Id,
                SchemaVersion = 1, Type = input.Type.Value, EventId = payload.EventId,
                InAppNotificationId = notification.Id,
                PayloadJson = JsonSerializer.Serialize(payload, SharedSerializationOptions.Current)
            });
            await database.SaveChangesAsync();
        }
        return new DeliveryScenario(input, assignment.Id, assignment.CurrentReportRequestId, messageId);
    }

    private WebApplicationFactory<Program> CreateDeliveryRaceFactory(DeliveryGate gate, bool remotePush)
    {
        var publisher = Substitute.For<IInAppNotificationPushPublisher>();
        publisher.PushAsync(Arg.Any<InAppNotificationResult>(), Arg.Any<CancellationToken>())
            .Returns(call => gate.SendAsync(call.Arg<CancellationToken>()));
        var sender = Substitute.For<IPushProviderSender>();
        sender.SendAsync(Arg.Any<PushInstallationDeliveryTarget>(), Arg.Any<PushEventPayload>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await gate.SendAsync(call.Arg<CancellationToken>());
                return new PushSendAttemptResult(PushSendOutcome.Sent, "Sent", "test-message", null, null);
            });
        return Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInAppNotificationPushPublisher>();
            services.RemoveAll<IPushProviderSender>();
            services.RemoveAll<INotificationEventBridge>();
            services.AddSingleton(publisher);
            services.AddSingleton(sender);
            services.AddSingleton(Substitute.For<INotificationEventBridge>());
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(provider => new DeliveryGateUnitOfWork(
                new EfUnitOfWork(provider.GetRequiredService<AppDbContext>()), gate, remotePush ? 1 : 2));
        }));
    }

    private static async Task InvalidateReminderAsync(
        IServiceProvider services, DeliveryScenario scenario, bool upcoming, bool unlink, CancellationToken ct)
    {
        if (unlink)
        {
            var result = await services.GetRequiredService<IUnlinkTraineeUseCase>().ExecuteAsync(
                new UnlinkTraineeCommand(scenario.Input.SenderUserId!.Value, scenario.Input.RecipientId), ct);
            result.IsSuccess.Should().BeTrue();
            return;
        }
        var database = services.GetRequiredService<AppDbContext>();
        if (upcoming)
        {
            var assignment = await database.RecurringReportAssignments.SingleAsync(item => item.Id == scenario.AssignmentId, ct);
            assignment.ScheduleRevision++;
        }
        else
        {
            var request = await database.ReportRequests.SingleAsync(item => item.Id == scenario.RequestId, ct);
            request.Status = ReportRequestStatus.Cancelled;
        }
        await database.SaveChangesAsync(ct);
    }

    private sealed class DeliveryGate
    {
        public bool PauseFence { get; init; }
        public bool PauseSend { get; init; }
        public int SendCount { get; private set; }
        public TaskCompletionSource FenceReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFence { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SendReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SendAsync(CancellationToken ct)
        {
            SendCount++;
            SendReached.TrySetResult();
            if (PauseSend) await ReleaseSend.Task.WaitAsync(ct);
        }
    }

    private sealed class DeliveryGateUnitOfWork(IUnitOfWork inner, DeliveryGate gate, int deliveryTransaction) : IUnitOfWork
    {
        private int _transactionCount;

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);

        public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            if (++_transactionCount == deliveryTransaction && gate.PauseFence)
            {
                gate.FenceReached.TrySetResult();
                await gate.ReleaseFence.Task.WaitAsync(ct);
            }
            return await inner.BeginTransactionAsync(ct);
        }
    }
}
