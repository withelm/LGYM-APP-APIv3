using System.Globalization;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using LgymApi.UnitTests.Fakes;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CoachingNotificationIntentInAppTests
{
    [Test]
    public async Task SubmitAsync_InvitationCreatedInApp_UsesLegacyDeliveryMetadata()
    {
        var harness = new TestHarness();
        var invitationId = Id<TrainerInvitation>.New();
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();

        var result = await harness.Service.SubmitAsync(new InvitationCreatedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.InApp, invitationId, trainerId, traineeId, "invitee@example.com", "code", DateTimeOffset.UtcNow,
            Account(trainerId, "Coach"), Account(traineeId, "Trainee")));

        Assert.That(result.EmailSchedulingRequest, Is.Null);
        var assertedValue87 = (harness.Inputs).ToArray();
        Assert.That(assertedValue87, Has.Exactly(1).Items);
        Assert.That(assertedValue87.Single(), Is.EqualTo(new CreateInAppNotificationInput(
            traineeId, trainerId, $"trainer-invitation:{invitationId}:sent", false,
            "Coach invited you to work together.", $"/trainers/invitations/{invitationId}", InAppNotificationTypes.InvitationSent)).UsingPropertiesComparer());
    }

    [Test]
    public async Task SubmitAsync_InvitationAcceptedInApp_UsesLegacyDeliveryMetadata()
    {
        var harness = new TestHarness();
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();

        var result = await harness.Service.SubmitAsync(new InvitationAcceptedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.InApp, invitationId, trainerId, traineeId, null, null));

        Assert.That(result.EmailSchedulingRequest, Is.Null);
        var assertedValue88 = (harness.Inputs).ToArray();
        Assert.That(assertedValue88, Has.Exactly(1).Items);
        Assert.That(assertedValue88.Single(), Is.EqualTo(new CreateInAppNotificationInput(
            trainerId, traineeId, $"trainer-invitation:{invitationId}:accepted", false,
            "A trainee has accepted your invitation.", $"/trainer/members/{traineeId}", InAppNotificationTypes.InvitationAccepted)).UsingPropertiesComparer());
    }

    [Test]
    public async Task SubmitAsync_InvitationRejectedInApp_UsesLegacyDeliveryMetadata()
    {
        var harness = new TestHarness();
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();

        await harness.Service.SubmitAsync(new InvitationRejectedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.InApp, invitationId, trainerId, traineeId));

        var assertedValue89 = (harness.Inputs).ToArray();
        Assert.That(assertedValue89, Has.Exactly(1).Items);
        Assert.That(assertedValue89.Single(), Is.EqualTo(new CreateInAppNotificationInput(
            trainerId, traineeId, $"trainer-invitation:{invitationId}:rejected", false,
            "A trainee has rejected your invitation.", "/trainer/invitations", InAppNotificationTypes.InvitationRejected)).UsingPropertiesComparer());
    }

    [Test]
    public async Task SubmitAsync_RelationshipEndedInApp_UsesRecipientCultureAndDeletedAccountFallback()
    {
        var harness = new TestHarness();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();

        await ExecuteWithCurrentUICultureAsync("en-US", () => harness.Service.SubmitAsync(new RelationshipEndedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.InApp, trainerId, traineeId, Account(trainerId, "Coach", culture: "pl-PL"), null)));

        var assertedValue90 = (harness.Inputs).ToArray();
        Assert.That(assertedValue90, Has.Exactly(1).Items);
        Assert.That(assertedValue90.Single(), Is.EqualTo(new CreateInAppNotificationInput(
            trainerId, traineeId, $"trainer-relationship-ended:{trainerId}:{traineeId}", false,
            "Podopieczny zakończył współpracę.", "/trainer/members", InAppNotificationTypes.TrainerRelationshipEnded)).UsingPropertiesComparer());
        Assert.That(harness.Inputs[0].Message, Does.Not.Contain("Trainee"));
    }

    [Test]
    public async Task SubmitAsync_RelationshipEndedInApp_UsesDefaultCultureWhenRecipientAccountIsMissing()
    {
        var harness = new TestHarness();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var previousCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var expectedMessage = string.Format(Messages.TrainerRelationshipEnded, Messages.GenericTraineeDisplayName);
        CultureInfo.CurrentUICulture = previousCulture;

        await ExecuteWithCurrentUICultureAsync("pl-PL", () => harness.Service.SubmitAsync(new RelationshipEndedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.InApp, trainerId, traineeId, null, null)));

        var assertedValue91 = (harness.Inputs).ToArray();
        Assert.That(assertedValue91, Has.Exactly(1).Items);
        Assert.That(assertedValue91.Single().Message, Is.EqualTo(expectedMessage));
    }

    [Test]
    public async Task SubmitAsync_TraineeNoteUpdatedInApp_UsesRecipientCultureAndFallbackNames()
    {
        var harness = new TestHarness();
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var noteId = Id<TraineeNote>.New();
        var triggeredAt = new DateTimeOffset(2026, 6, 26, 0, 30, 0, TimeSpan.Zero);

        await ExecuteWithCurrentUICultureAsync("en-US", () => harness.Service.SubmitAsync(new TraineeNoteUpdatedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.InApp, noteId, traineeId, trainerId, "  ", triggeredAt, null, Account(traineeId, "Trainee", culture: "pl-PL"))));

        var assertedValue92 = (harness.Inputs).ToArray();
        Assert.That(assertedValue92, Has.Exactly(1).Items);
        Assert.That(assertedValue92.Single(), Is.EqualTo(new CreateInAppNotificationInput(
            traineeId, trainerId, $"trainee-note:{noteId}:{triggeredAt:O}", false,
            "Trener zaktualizował Twoją notatkę: notatka trenera", $"/trainer/notes/{noteId}", InAppNotificationTypes.TraineeNoteUpdated)).UsingPropertiesComparer());
        Assert.That(harness.Inputs[0].Message, Does.Not.Contain("Trainer"));
        Assert.That(harness.Inputs[0].Message, Does.Not.Contain("trainer note"));
    }

    [TestCase(CoachingNotificationLegacyChannel.Email)]
    public async Task SubmitAsync_IneligibleInAppIntent_RejectsWrongChannel(CoachingNotificationLegacyChannel channel)
    {
        var harness = new TestHarness();

        var action = () => harness.Service.SubmitAsync(new InvitationRejectedCoachingNotificationIntent(
            channel, Id<TrainerInvitation>.New(), Id<User>.New(), Id<User>.New()));

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<ArgumentException>());
        Assert.That(harness.Inputs, Is.Empty);
    }

    [Test]
    public async Task SubmitAsync_InvitationCreatedInAppWithoutRecipient_RejectsTheIntent()
    {
        var harness = new TestHarness();

        var action = () => harness.Service.SubmitAsync(new InvitationCreatedCoachingNotificationIntent(
            CoachingNotificationLegacyChannel.InApp,
            Id<TrainerInvitation>.New(),
            Id<User>.New(),
            null,
            "invitee@example.com",
            "code",
            DateTimeOffset.UtcNow,
            null,
            null));

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<ArgumentException>());
        Assert.That(harness.Inputs, Is.Empty);
    }

    private static AccountReadModel Account(Id<User> id, string name, string email = "person@example.com", string culture = "en-US")
        => new(id, name, email, null, culture, "Europe/Warsaw");

    private static async Task ExecuteWithCurrentUICultureAsync(string cultureName, Func<Task> action)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    private sealed class TestHarness
    {
        public TestHarness(string preferredLanguage = "en-US")
        {
            InAppNotificationService.CreateAsync(Arg.Any<CreateInAppNotificationInput>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    Inputs.Add(call.ArgAt<CreateInAppNotificationInput>(0));
                    return Task.FromResult(Result<InAppNotificationResult, AppError>.Success(new InAppNotificationResult(
                        Id<InAppNotification>.New(), Id<User>.New(), "message", null, false, InAppNotificationTypes.InvitationSent, false, null, DateTimeOffset.UtcNow)));
                });
            EmailNotificationFeature.Enabled.Returns(true);
            Service = new CoachingNotificationIntentService(
                InAppNotificationService,
                EmailNotificationLogRepository,
                EmailNotificationFeature,
                new AppDefaultsOptions { PreferredLanguage = preferredLanguage, PreferredTimeZone = "Europe/Warsaw" });
        }

        public List<CreateInAppNotificationInput> Inputs { get; } = [];
        public IInAppNotificationService InAppNotificationService { get; } = Substitute.For<IInAppNotificationService>();
        public ConfigurableEmailNotificationLogRepository EmailNotificationLogRepository { get; } = new();
        public ICoachingEmailNotificationFeature EmailNotificationFeature { get; } = Substitute.For<ICoachingEmailNotificationFeature>();
        public CoachingNotificationIntentService Service { get; }
    }
}
