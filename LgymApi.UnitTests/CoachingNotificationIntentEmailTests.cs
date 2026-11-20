using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.UnitTests.Fakes;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CoachingNotificationIntentEmailTests
{
    [Test]
    public async Task SubmitAsync_InvitationCreatedEmail_CreatesLegacySchedulingRequestWithoutInAppFanout()
    {
        var harness = new TestHarness();
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var expiresAt = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

        var result = await harness.Service.SubmitAsync(new InvitationCreatedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.Email, invitationId, trainerId, traineeId, "invitee@example.com", "ABC123", expiresAt,
            Account(trainerId, "Coach", "coach@example.com", "pl-PL", "Europe/Warsaw"),
            Account(traineeId, "Trainee", "trainee@example.com", "en-US", "Europe/Madrid")));

        Assert.That(result.EmailSchedulingRequest, Is.EqualTo(new CoachingEmailSchedulingRequest(
            CoachingEmailSchedulingKind.InvitationCreated, EmailNotificationTypes.TrainerInvitation, invitationId,
            invitationId.Rebind<CorrelationScope>(), "trainee@example.com", "pl-PL", "Europe/Madrid", "Coach", null, "ABC123", expiresAt)).UsingPropertiesComparer());
        await harness.InAppNotificationService.DidNotReceive().CreateAsync(Arg.Any<CreateInAppNotificationInput>(), Arg.Any<CancellationToken>());
        Assert.That(harness.EmailNotificationLogRepository.Calls
            .Where(call =>
                call.Method == nameof(IEmailNotificationLogRepository.FindByCorrelationAsync)
                && call.Argument is ValueTuple<EmailNotificationType, Id<CorrelationScope>, string> arguments
                && arguments.Item1 == EmailNotificationTypes.TrainerInvitation
                && arguments.Item2 == invitationId.Rebind<CorrelationScope>()
                && arguments.Item3 == "trainee@example.com"), Has.Exactly(1).Items);
    }

    [Test]
    public async Task SubmitAsync_InvitationAcceptedEmail_CreatesLegacySchedulingRequestWithoutInAppFanout()
    {
        var harness = new TestHarness();
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();

        var result = await harness.Service.SubmitAsync(new InvitationAcceptedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.Email, invitationId, trainerId, traineeId,
            Account(trainerId, "Coach", "coach@example.com", "pl-PL", "Europe/Warsaw"),
            Account(traineeId, "Trainee", "trainee@example.com")));

        Assert.That(result.EmailSchedulingRequest, Is.EqualTo(new CoachingEmailSchedulingRequest(
            CoachingEmailSchedulingKind.InvitationAccepted, EmailNotificationTypes.TrainerInvitationAccepted, invitationId,
            invitationId.Rebind<CorrelationScope>(), "coach@example.com", "pl-PL", "Europe/Warsaw", "Coach", "Trainee", null, null)).UsingPropertiesComparer());
        await harness.InAppNotificationService.DidNotReceive().CreateAsync(Arg.Any<CreateInAppNotificationInput>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SubmitAsync_InvitationRevokedEmail_UsesDefaultCultureAndLegacyIdentity()
    {
        var harness = new TestHarness();
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();

        var result = await harness.Service.SubmitAsync(new InvitationRevokedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.Email, invitationId, trainerId, "invitee@example.com", Account(trainerId, "Coach", culture: "pl-PL")));

        Assert.That(result.EmailSchedulingRequest, Is.EqualTo(new CoachingEmailSchedulingRequest(
            CoachingEmailSchedulingKind.InvitationRevoked, EmailNotificationTypes.TrainerInvitationRevoked, invitationId,
            invitationId.Rebind<CorrelationScope>(), "invitee@example.com", "en-US", "Europe/Warsaw", "Coach", null, null, null)).UsingPropertiesComparer());
        await harness.InAppNotificationService.DidNotReceive().CreateAsync(Arg.Any<CreateInAppNotificationInput>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SubmitAsync_MissingAccountOrDuplicateEmailIdentity_SuppressesScheduling()
    {
        var harness = new TestHarness();
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        harness.EmailNotificationLogRepository.FindByCorrelation = (_, _, _, _) => Task.FromResult<NotificationMessage?>(new NotificationMessage
            {
                Status = EmailNotificationStatus.Sent,
                Attempts = 0
            });

        var missingAccount = await harness.Service.SubmitAsync(new InvitationCreatedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.Email, invitationId, trainerId, traineeId, "invitee@example.com", "code", DateTimeOffset.UtcNow,
            Account(trainerId, "Coach"), null));
        var duplicate = await harness.Service.SubmitAsync(new InvitationAcceptedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.Email, invitationId, trainerId, traineeId,
            Account(trainerId, "Coach", "coach@example.com"), Account(traineeId, "Trainee")));

        Assert.That(missingAccount.EmailSchedulingRequest, Is.Null);
        Assert.That(duplicate.EmailSchedulingRequest, Is.Null);
        await harness.InAppNotificationService.DidNotReceive().CreateAsync(Arg.Any<CreateInAppNotificationInput>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SubmitAsync_InvitationCreatedEmail_WhenFeatureDisabledOrRetryLimitReached_SuppressesScheduling()
    {
        var harness = new TestHarness();
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var intent = new InvitationCreatedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.Email,
            invitationId,
            trainerId,
            traineeId,
            "invitee@example.com",
            "code",
            DateTimeOffset.UtcNow,
            Account(trainerId, "Coach"),
            Account(traineeId, "Trainee", "trainee@example.com"));
        harness.EmailNotificationFeature.Enabled.Returns(false);

        var featureDisabled = await harness.Service.SubmitAsync(intent);

        Assert.That(featureDisabled.EmailSchedulingRequest, Is.Null);
        Assert.That((harness.EmailNotificationLogRepository.Calls).Any(call => call.Method == nameof(IEmailNotificationLogRepository.FindByCorrelationAsync)), Is.False);
        harness.EmailNotificationFeature.Enabled.Returns(true);
        harness.EmailNotificationLogRepository.FindByCorrelation = (_, _, _, _) => Task.FromResult<NotificationMessage?>(new NotificationMessage
            {
                Status = EmailNotificationStatus.Failed,
                Attempts = 5
            });

        var retryLimitReached = await harness.Service.SubmitAsync(intent);

        Assert.That(retryLimitReached.EmailSchedulingRequest, Is.Null);
        await harness.InAppNotificationService.DidNotReceive().CreateAsync(Arg.Any<CreateInAppNotificationInput>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SubmitAsync_InvitationCreatedEmail_WhenRecipientEmailIsBlank_SuppressesScheduling()
    {
        var harness = new TestHarness();
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();

        var result = await harness.Service.SubmitAsync(new InvitationCreatedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.Email,
            invitationId,
            trainerId,
            traineeId,
            "invitee@example.com",
            "code",
            DateTimeOffset.UtcNow,
            Account(trainerId, "Coach"),
            Account(traineeId, "Trainee", "   ")));

        Assert.That(result.EmailSchedulingRequest, Is.Null);
        await harness.InAppNotificationService.DidNotReceive().CreateAsync(Arg.Any<CreateInAppNotificationInput>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SubmitAsync_IneligibleEmailIntent_RejectsWrongChannel()
    {
        var harness = new TestHarness();

        var action = () => harness.Service.SubmitAsync(new InvitationRevokedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.InApp, Id<TrainerInvitation>.New(), Id<User>.New(), "invitee@example.com", null));

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<ArgumentException>());
    }

    private static AccountReadModel Account(
        Id<User> id,
        string name,
        string email = "person@example.com",
        string culture = "en-US",
        string timeZone = "Europe/Warsaw")
        => new(id, name, email, null, culture, timeZone);

    private sealed class TestHarness
    {
        public TestHarness()
        {
            InAppNotificationService.CreateAsync(Arg.Any<CreateInAppNotificationInput>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result<InAppNotificationResult, AppError>.Success(new InAppNotificationResult(
                    Id<InAppNotification>.New(), Id<User>.New(), "message", null, false, InAppNotificationTypes.InvitationSent, false, null, DateTimeOffset.UtcNow))));
            EmailNotificationFeature.Enabled.Returns(true);
            Service = new CoachingNotificationIntentService(
                InAppNotificationService,
                EmailNotificationLogRepository,
                EmailNotificationFeature,
                new AppDefaultsOptions { PreferredLanguage = "en-US", PreferredTimeZone = "Europe/Warsaw" });
        }

        public IInAppNotificationService InAppNotificationService { get; } = Substitute.For<IInAppNotificationService>();
        public ConfigurableEmailNotificationLogRepository EmailNotificationLogRepository { get; } = new();
        public ICoachingEmailNotificationFeature EmailNotificationFeature { get; } = Substitute.For<ICoachingEmailNotificationFeature>();
        public CoachingNotificationIntentService Service { get; }
    }
}
