using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.BackgroundWorker.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Notifications.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PushNotificationPipelineIntegrationTests
{
    [Test]
    public async Task EnqueueAsync_PersistsPendingAndSkippedRows_ByInstallationEligibility()
    {
        await using var factory = new CustomWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await TestUtils.TestDataFactory.SeedUserAsync(db, "push-int-user", "push-int-user@example.com", "pass123");
            db.PushInstallations.AddRange(
                new PushInstallation
                {
                    Id = Id<PushInstallation>.New(),
                    UserId = user.Id,
                    InstallationId = "device-active",
                    Platform = "android",
                    FcmToken = "token-active",
                    Environment = "development",
                    PermissionStatus = "authorized",
                    LastSeenAt = DateTimeOffset.UtcNow
                },
                new PushInstallation
                {
                    Id = Id<PushInstallation>.New(),
                    UserId = user.Id,
                    InstallationId = "device-denied",
                    Platform = "ios",
                    FcmToken = "token-denied",
                    Environment = "development",
                    PermissionStatus = "denied",
                    LastSeenAt = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await service.EnqueueAsync(new Application.Notifications.Models.EnqueuePushNotificationInput(
                user.Id,
                1,
                "trainer.note.updated",
                "event-integration-1",
                null,
                null,
                null));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = db.PushNotificationMessages.OrderBy(x => x.EventId).ToList();
            rows.Should().HaveCount(2);
            rows.Should().ContainSingle(x => x.Status == PushNotificationStatus.Pending);
            rows.Should().ContainSingle(x => x.Status == PushNotificationStatus.Skipped && x.FailureKind == PushNotificationFailureKind.Preference);
        }
    }

    [Test]
    public async Task ProcessAsync_WithInjectedInvalidTokenSender_DisablesInstallationAndPersistsFailure()
    {
        var fakeSender = new InvalidTokenPushProviderSender();
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPushProviderSender>();
                services.AddSingleton<IPushProviderSender>(fakeSender);
            });
        });

        Id<PushNotificationMessage> notificationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await TestUtils.TestDataFactory.SeedUserAsync(db, "push-int-send", "push-int-send@example.com", "pass123");
            var installation = new PushInstallation
            {
                Id = Id<PushInstallation>.New(),
                UserId = user.Id,
                InstallationId = "device-invalid",
                Platform = "android",
                FcmToken = "token-invalid",
                Environment = "development",
                PermissionStatus = "authorized",
                LastSeenAt = DateTimeOffset.UtcNow
            };
            db.PushInstallations.Add(installation);
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await service.EnqueueAsync(new Application.Notifications.Models.EnqueuePushNotificationInput(
                user.Id,
                1,
                "trainer.note.updated",
                "event-integration-2",
                null,
                null,
                null));

            notificationId = db.PushNotificationMessages.Single().Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PushNotificationJobHandlerService>();
            await handler.ProcessAsync(notificationId);
        }

        fakeSender.LastTarget.Should().NotBeNull();
        fakeSender.LastTarget!.InstallationId.Should().Be("device-invalid");
        fakeSender.LastTarget.DeviceToken.Should().Be("token-invalid");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var installation = db.PushInstallations.Single(x => x.InstallationId == "device-invalid");
            var message = db.PushNotificationMessages.Single(x => x.Id == notificationId);

            installation.DisabledReason.Should().Be("InvalidFcmToken");
            message.Status.Should().Be(PushNotificationStatus.Failed);
            message.FailureKind.Should().Be(PushNotificationFailureKind.InvalidToken);
            message.NextAttemptAt.Should().BeNull();
        }
    }

    private sealed class InvalidTokenPushProviderSender : IPushProviderSender
    {
        public PushDeliveryTarget? LastTarget { get; private set; }

        public Task<PushSendAttemptResult> SendAsync(PushDeliveryTarget target, PushEventPayload payload, CancellationToken cancellationToken = default)
        {
            LastTarget = target;
            return Task.FromResult(new PushSendAttemptResult(
                PushSendOutcome.InvalidToken,
                "BadRequest",
                null,
                "UNREGISTERED",
                "registration-token-not-registered"));
        }
    }
}
