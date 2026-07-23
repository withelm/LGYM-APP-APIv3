using FluentAssertions;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Coaching.Contracts.Notifications;
using LgymApi.Application.Identity.Contracts.Accounts;
using LgymApi.Application.Notifications.Contracts.Events;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SendInvitationEmailHandlerTests
{
    private ICoachingNotificationReadService _notificationReads = null!;
    private IAccountReadService _accounts = null!;
    private ICoachingNotificationIntentService _notificationIntents = null!;
    private ICoachingEmailNotificationScheduler _scheduler = null!;
    private SendInvitationEmailHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _notificationReads = Substitute.For<ICoachingNotificationReadService>();
        _accounts = Substitute.For<IAccountReadService>();
        _notificationIntents = Substitute.For<ICoachingNotificationIntentService>();
        _scheduler = Substitute.For<ICoachingEmailNotificationScheduler>();
        _notificationIntents.SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CoachingNotificationIntentResult(null, null)));
        _handler = new SendInvitationEmailHandler(
            _notificationReads,
            _accounts,
            _notificationIntents,
            _scheduler,
            NullLogger<SendInvitationEmailHandler>.Instance);
    }

    [Test]
    public async Task ExecuteAsync_SubmitsCreatedEmailIntentAndSchedulesLegacyPayload()
    {
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var expiresAt = new DateTimeOffset(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);
        var trainer = Account(trainerId, "Coach", "coach@example.com", "pl-PL", "Europe/Warsaw");
        var trainee = Account(traineeId, "Trainee", "trainee@example.com", "en-US", "Europe/Madrid");
        var request = new CoachingEmailSchedulingRequest(
            CoachingEmailSchedulingKind.InvitationCreated,
            EmailNotificationTypes.TrainerInvitation,
            invitationId,
            invitationId.Rebind<CorrelationScope>(),
            trainee.Email,
            trainer.PreferredLanguage,
            trainee.PreferredTimeZone,
            trainer.Name,
            null,
            "CODE123",
            expiresAt);
        _notificationReads.GetInvitationAsync(invitationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoachingInvitationNotificationFact?>(new(
                invitationId, trainerId, traineeId, "invitee@example.com", "CODE123", expiresAt)));
        _accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(trainer));
        _accounts.GetByIdAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(trainee));
        _notificationIntents.SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CoachingNotificationIntentResult(request, null)));
        using var cancellation = new CancellationTokenSource();

        await _handler.ExecuteAsync(new InvitationCreatedCommand { InvitationId = invitationId }, cancellation.Token);

        await _notificationIntents.Received(1).SubmitAsync(
            Arg.Is<CoachingNotificationIntent>(intent => IsCreatedEmailIntent(intent, invitationId, trainerId, traineeId, expiresAt, trainer, trainee)),
            cancellation.Token);
        await _scheduler.Received(1).ScheduleAsync(
            request,
            cancellation.Token);
    }

    [Test]
    public async Task ExecuteAsync_WhenInvitationIsMissing_DoesNotSubmitAnIntent()
    {
        var invitationId = Id<TrainerInvitation>.New();
        _notificationReads.GetInvitationAsync(invitationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoachingInvitationNotificationFact?>(null));

        await _handler.ExecuteAsync(new InvitationCreatedCommand { InvitationId = invitationId });

        await _accounts.DidNotReceive().GetByIdAsync(Arg.Any<Id<User>>(), Arg.Any<CancellationToken>());
        await _notificationIntents.DidNotReceive().SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceive().ScheduleAsync(Arg.Any<CoachingEmailSchedulingRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenBoundTraineeAccountIsMissing_DoesNotSubmitAnIntent()
    {
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        _notificationReads.GetInvitationAsync(invitationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoachingInvitationNotificationFact?>(new(
                invitationId, trainerId, traineeId, "invitee@example.com", "CODE123", DateTimeOffset.UtcNow)));
        _accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccountReadModel?>(Account(trainerId, "Coach", "coach@example.com")));
        _accounts.GetByIdAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(null));

        await _handler.ExecuteAsync(new InvitationCreatedCommand { InvitationId = invitationId });

        await _notificationIntents.DidNotReceive().SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceive().ScheduleAsync(Arg.Any<CoachingEmailSchedulingRequest>(), Arg.Any<CancellationToken>());
    }

    private static AccountReadModel Account(
        Id<User> id,
        string name,
        string email,
        string culture = "en-US",
        string timeZone = "Europe/Warsaw") => new(id, name, email, null, culture, timeZone);

    private static bool IsCreatedEmailIntent(
        CoachingNotificationIntent intent,
        Id<TrainerInvitation> invitationId,
        Id<User> trainerId,
        Id<User> traineeId,
        DateTimeOffset expiresAt,
        AccountReadModel trainer,
        AccountReadModel trainee)
    {
        return intent is InvitationCreatedCoachingNotificationIntent created
            && created.EligibleLegacyChannel == CoachingNotificationLegacyChannel.Email
            && created.InvitationId == invitationId
            && created.TrainerId == trainerId
            && created.TraineeId == traineeId
            && created.InviteeEmail == "invitee@example.com"
            && created.InvitationCode == "CODE123"
            && created.ExpiresAt == expiresAt
            && created.Trainer == trainer
            && created.Trainee == trainee;
    }
}
