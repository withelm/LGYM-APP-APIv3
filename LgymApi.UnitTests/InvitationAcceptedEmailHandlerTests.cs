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
public sealed class InvitationAcceptedEmailHandlerTests
{
    private ICoachingNotificationReadService _notificationReads = null!;
    private IAccountReadService _accounts = null!;
    private ICoachingNotificationIntentService _notificationIntents = null!;
    private ICoachingEmailNotificationScheduler _scheduler = null!;
    private InvitationAcceptedEmailHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _notificationReads = Substitute.For<ICoachingNotificationReadService>();
        _accounts = Substitute.For<IAccountReadService>();
        _notificationIntents = Substitute.For<ICoachingNotificationIntentService>();
        _scheduler = Substitute.For<ICoachingEmailNotificationScheduler>();
        _notificationIntents.SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CoachingNotificationIntentResult(null, null)));
        _handler = new InvitationAcceptedEmailHandler(
            _notificationReads,
            _accounts,
            _notificationIntents,
            _scheduler,
            NullLogger<InvitationAcceptedEmailHandler>.Instance);
    }

    [Test]
    public async Task ExecuteAsync_SubmitsAcceptedEmailIntentAndSchedulesLegacyPayload()
    {
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var trainer = Account(trainerId, "Coach", "coach@example.com", "pl-PL", "Europe/Warsaw");
        var trainee = Account(traineeId, "Trainee", "trainee@example.com", "en-US", "Europe/Madrid");
        var request = new CoachingEmailSchedulingRequest(
            CoachingEmailSchedulingKind.InvitationAccepted,
            EmailNotificationTypes.TrainerInvitationAccepted,
            invitationId,
            invitationId.Rebind<CorrelationScope>(),
            trainer.Email,
            trainer.PreferredLanguage,
            trainer.PreferredTimeZone,
            trainer.Name,
            trainee.Name,
            null,
            null);
        _notificationReads.GetInvitationAsync(invitationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoachingInvitationNotificationFact?>(new(
                invitationId, trainerId, traineeId, "invitee@example.com", "CODE123", DateTimeOffset.UtcNow)));
        _accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(trainer));
        _accounts.GetByIdAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(trainee));
        _notificationIntents.SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CoachingNotificationIntentResult(request, null)));
        using var cancellation = new CancellationTokenSource();

        await _handler.ExecuteAsync(new InvitationAcceptedCommand { InvitationId = invitationId }, cancellation.Token);

        await _notificationIntents.Received(1).SubmitAsync(
            Arg.Is<CoachingNotificationIntent>(intent => IsAcceptedEmailIntent(intent, invitationId, trainerId, traineeId, trainer, trainee)),
            cancellation.Token);
        await _scheduler.Received(1).ScheduleAsync(
            request,
            cancellation.Token);
    }

    [Test]
    public async Task ExecuteAsync_WhenInvitationHasNoBoundTrainee_DoesNotSubmitAnIntent()
    {
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        _notificationReads.GetInvitationAsync(invitationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoachingInvitationNotificationFact?>(new(
                invitationId, trainerId, null, "invitee@example.com", "CODE123", DateTimeOffset.UtcNow)));
        _accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccountReadModel?>(Account(trainerId, "Coach", "coach@example.com")));

        await _handler.ExecuteAsync(new InvitationAcceptedCommand { InvitationId = invitationId });

        await _accounts.Received(1).GetByIdAsync(trainerId, Arg.Any<CancellationToken>());
        await _notificationIntents.DidNotReceive().SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceive().ScheduleAsync(Arg.Any<CoachingEmailSchedulingRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenNotificationsSuppressMissingTrainerEmail_DoesNotSchedule()
    {
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var trainer = Account(trainerId, "Coach", string.Empty);
        var trainee = Account(traineeId, "Trainee", "trainee@example.com");
        _notificationReads.GetInvitationAsync(invitationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoachingInvitationNotificationFact?>(new(
                invitationId, trainerId, traineeId, "invitee@example.com", "CODE123", DateTimeOffset.UtcNow)));
        _accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(trainer));
        _accounts.GetByIdAsync(traineeId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(trainee));

        await _handler.ExecuteAsync(new InvitationAcceptedCommand { InvitationId = invitationId });

        await _notificationIntents.Received(1).SubmitAsync(
            Arg.Is<CoachingNotificationIntent>(intent => intent is InvitationAcceptedCoachingNotificationIntent),
            Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceive().ScheduleAsync(Arg.Any<CoachingEmailSchedulingRequest>(), Arg.Any<CancellationToken>());
    }

    private static AccountReadModel Account(
        Id<User> id,
        string name,
        string email,
        string culture = "en-US",
        string timeZone = "Europe/Warsaw") => new(id, name, email, null, culture, timeZone);

    private static bool IsAcceptedEmailIntent(
        CoachingNotificationIntent intent,
        Id<TrainerInvitation> invitationId,
        Id<User> trainerId,
        Id<User> traineeId,
        AccountReadModel trainer,
        AccountReadModel trainee)
    {
        return intent is InvitationAcceptedCoachingNotificationIntent accepted
            && accepted.EligibleLegacyChannel == CoachingNotificationLegacyChannel.Email
            && accepted.InvitationId == invitationId
            && accepted.TrainerId == trainerId
            && accepted.TraineeId == traineeId
            && accepted.Trainer == trainer
            && accepted.Trainee == trainee;
    }
}
