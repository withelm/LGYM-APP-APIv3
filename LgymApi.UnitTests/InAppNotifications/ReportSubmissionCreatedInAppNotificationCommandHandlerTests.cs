using LgymApi.Authorization.Contracts;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.InApp;
using LgymApi.Application.Notifications.InApp;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Globalization;
using System.Text.Json;
using LgymApi.Application.Platform.Contracts.Serialization;

namespace LgymApi.UnitTests.InAppNotifications;

[TestFixture]
public sealed class ReportSubmissionCreatedInAppNotificationCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_Success_CreatesExpectedNotification()
    {
        var service = new FakeNotificationService(Result<InAppNotificationResult, AppError>.Success(CreateResult()));
        var trainerId = Id<AccountReference>.New();
        var traineeId = Id<AccountReference>.New();
        var submissionId = Id<ReportSubmission>.New();
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var owner = new ReportSubmissionCreatedActionExecutionPort(
            service,
            new FakeAccountLookupService(
                ReportingTestData.Lookup(trainerId, "Coach", "en-US"),
                ReportingTestData.Lookup(traineeId, "Adam")),
            new AppDefaultsOptions { PreferredLanguage = "pl-PL" });

        try
        {
            await owner.ExecuteAsync(JsonSerializer.Serialize(new ReportSubmissionCreatedInAppNotificationCommand
            {
                SubmissionId = submissionId,
                TrainerId = trainerId,
                TraineeId = traineeId,
                TemplateName = "Weekly check-in"
            }, SharedSerializationOptions.Current));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }

        Assert.That(service.Calls, Is.EqualTo(1));
        Assert.That(service.LastInput, Is.Not.Null);
        Assert.That(service.LastInput!.RecipientId, Is.EqualTo(trainerId.Rebind<User>()));
        Assert.That(service.LastInput.SenderUserId, Is.EqualTo(traineeId.Rebind<User>()));
        Assert.That(service.LastInput.DeliveryKey, Is.EqualTo($"report-submission:{submissionId}"));
        Assert.That(service.LastInput.Message, Is.EqualTo("Adam submitted a report: Weekly check-in."));
        Assert.That(service.LastInput.RedirectUrl, Is.EqualTo($"/trainer/members/{traineeId}?tab=reports&submissionId={submissionId}"));
        Assert.That(service.LastInput.Type, Is.EqualTo(InAppNotificationTypes.ReportSubmissionReceived));
    }

    [Test]
    public async Task ExecuteAsync_WhenNamesMissing_UsesLocalizedFallbacks()
    {
        var service = new FakeNotificationService(Result<InAppNotificationResult, AppError>.Success(CreateResult()));
        var trainerId = Id<AccountReference>.New();
        var traineeId = Id<AccountReference>.New();
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var owner = new ReportSubmissionCreatedActionExecutionPort(
            service,
            new FakeAccountLookupService(
                ReportingTestData.Lookup(trainerId, string.Empty, "pl-PL"),
                ReportingTestData.Lookup(traineeId, string.Empty)),
            new AppDefaultsOptions { PreferredLanguage = "en-US" });

        try
        {
            await owner.ExecuteAsync(JsonSerializer.Serialize(new ReportSubmissionCreatedInAppNotificationCommand
            {
                SubmissionId = Id<ReportSubmission>.New(),
                TrainerId = trainerId,
                TraineeId = traineeId,
                TemplateName = string.Empty
            }, SharedSerializationOptions.Current));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }

        Assert.That(service.LastInput!.Message, Is.EqualTo("Podopieczny wysłał raport: raport."));
    }

    [Test]
    public async Task ExecuteAsync_ForwardsCanonicalJsonToOwnerPort()
    {
        var port = new RecordingExecutionPort();
        var handler = new ReportSubmissionCreatedInAppNotificationCommandHandler(port);
        var command = new ReportSubmissionCreatedInAppNotificationCommand
        {
            SubmissionId = Id<ReportSubmission>.New(),
            TrainerId = Id<AccountReference>.New(),
            TraineeId = Id<AccountReference>.New(),
            TemplateName = "Weekly check-in"
        };

        await handler.ExecuteAsync(command);

        Assert.That(port.PayloadJson, Is.EqualTo(JsonSerializer.Serialize(command, SharedSerializationOptions.Current)));
    }

    private static InAppNotificationResult CreateResult()
        => new(Id<InAppNotification>.New(), Id<User>.New(), "message", null, false, InAppNotificationTypes.ReportSubmissionReceived, false, null, DateTimeOffset.UtcNow);

    private sealed class FakeNotificationService : IInAppNotificationWireWriter
    {
        private readonly Result<InAppNotificationResult, AppError> _result;

        public FakeNotificationService(Result<InAppNotificationResult, AppError> result) => _result = result;

        public int Calls { get; private set; }
        public CreateInAppNotificationInput? LastInput { get; private set; }

        public Task CreateAsync(string recipientId, string actorId, string deliveryKey, string message, string redirectUrl, string notificationType, CancellationToken cancellationToken = default)
        {
            Assert.That(Id<User>.TryParse(recipientId, out var recipient), Is.True);
            Assert.That(Id<User>.TryParse(actorId, out var actor), Is.True);
            Assert.That(InAppNotificationTypes.TryFromValue(notificationType, out var type), Is.True);
            Calls++;
            LastInput = new CreateInAppNotificationInput(recipient, actor, deliveryKey, false, message, redirectUrl, type);
            return Task.CompletedTask;
        }

        public Task<Result<PagedResult<InAppNotificationResult>, AppError>> GetForUserAsync(Id<User> userId, CursorPaginationQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<Unit, AppError>> MarkAsReadAsync(Id<InAppNotification> notificationId, AuthorizationActor actor, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<Unit, AppError>> MarkAllAsReadAsync(Id<User> userId, DateTimeOffset? before, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result<int, AppError>> GetUnreadCountAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class RecordingExecutionPort : IReportSubmissionCreatedActionExecutionPort
    {
        public string? PayloadJson { get; private set; }

        public Task ExecuteAsync(string payloadJson, CancellationToken cancellationToken = default)
        {
            PayloadJson = payloadJson;
            return Task.CompletedTask;
        }
    }

}
