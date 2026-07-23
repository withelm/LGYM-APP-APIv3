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
public sealed class InvitationRevokedEmailHandlerTests
{
    private ICoachingNotificationReadService _notificationReads = null!;
    private IAccountReadService _accounts = null!;
    private ICoachingNotificationIntentService _notificationIntents = null!;
    private ICoachingEmailNotificationScheduler _scheduler = null!;
    private InvitationRevokedEmailHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _notificationReads = Substitute.For<ICoachingNotificationReadService>();
        _accounts = Substitute.For<IAccountReadService>();
        _notificationIntents = Substitute.For<ICoachingNotificationIntentService>();
        _scheduler = Substitute.For<ICoachingEmailNotificationScheduler>();
        _notificationIntents.SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CoachingNotificationIntentResult(null, null)));
        _handler = new InvitationRevokedEmailHandler(
            _notificationReads,
            _accounts,
            _notificationIntents,
            _scheduler,
            NullLogger<InvitationRevokedEmailHandler>.Instance);
    }

    [Test]
    public async Task ExecuteAsync_SubmitsRevokedEmailIntentAndSchedulesLegacyPayload()
    {
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var trainer = new AccountReadModel(trainerId, "Coach", "coach@example.com", null, "pl-PL", "Europe/Warsaw");
        var request = new CoachingEmailSchedulingRequest(
            CoachingEmailSchedulingKind.InvitationRevoked,
            EmailNotificationTypes.TrainerInvitationRevoked,
            invitationId,
            invitationId.Rebind<CorrelationScope>(),
            "invitee@example.com",
            "en-US",
            "Europe/Warsaw",
            trainer.Name,
            null,
            null,
            null);
        _notificationReads.GetInvitationAsync(invitationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoachingInvitationNotificationFact?>(new(
                invitationId, trainerId, null, "invitee@example.com", "CODE123", DateTimeOffset.UtcNow)));
        _accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(trainer));
        _notificationIntents.SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CoachingNotificationIntentResult(request, null)));
        using var cancellation = new CancellationTokenSource();

        await _handler.ExecuteAsync(new InvitationRevokedCommand { InvitationId = invitationId }, cancellation.Token);

        await _notificationIntents.Received(1).SubmitAsync(
            Arg.Is<CoachingNotificationIntent>(intent => IsRevokedEmailIntent(intent, invitationId, trainerId, trainer)),
            cancellation.Token);
        await _scheduler.Received(1).ScheduleAsync(
            request,
            cancellation.Token);
    }

    [Test]
    public async Task ExecuteAsync_WhenTrainerAccountIsMissing_DoesNotSubmitAnIntent()
    {
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        _notificationReads.GetInvitationAsync(invitationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoachingInvitationNotificationFact?>(new(
                invitationId, trainerId, null, "invitee@example.com", "CODE123", DateTimeOffset.UtcNow)));
        _accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(null));

        await _handler.ExecuteAsync(new InvitationRevokedCommand { InvitationId = invitationId });

        await _notificationIntents.DidNotReceive().SubmitAsync(Arg.Any<CoachingNotificationIntent>(), Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceive().ScheduleAsync(Arg.Any<CoachingEmailSchedulingRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenNotificationsSuppressMissingInviteeEmail_DoesNotSchedule()
    {
        var invitationId = Id<TrainerInvitation>.New();
        var trainerId = Id<User>.New();
        var trainer = new AccountReadModel(trainerId, "Coach", "coach@example.com", null, "en-US", "Europe/Warsaw");
        _notificationReads.GetInvitationAsync(invitationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CoachingInvitationNotificationFact?>(new(
                invitationId, trainerId, null, string.Empty, "CODE123", DateTimeOffset.UtcNow)));
        _accounts.GetByIdAsync(trainerId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AccountReadModel?>(trainer));

        await _handler.ExecuteAsync(new InvitationRevokedCommand { InvitationId = invitationId });

        await _notificationIntents.Received(1).SubmitAsync(
            Arg.Is<CoachingNotificationIntent>(intent => intent is InvitationRevokedCoachingNotificationIntent),
            Arg.Any<CancellationToken>());
        await _scheduler.DidNotReceive().ScheduleAsync(Arg.Any<CoachingEmailSchedulingRequest>(), Arg.Any<CancellationToken>());
    }

    private static bool IsRevokedEmailIntent(
        CoachingNotificationIntent intent,
        Id<TrainerInvitation> invitationId,
        Id<User> trainerId,
        AccountReadModel trainer)
    {
        return intent is InvitationRevokedCoachingNotificationIntent revoked
            && revoked.EligibleLegacyChannel == CoachingNotificationLegacyChannel.Email
            && revoked.InvitationId == invitationId
            && revoked.TrainerId == trainerId
            && revoked.InviteeEmail == "invitee@example.com"
            && revoked.Trainer == trainer;
    }
}
