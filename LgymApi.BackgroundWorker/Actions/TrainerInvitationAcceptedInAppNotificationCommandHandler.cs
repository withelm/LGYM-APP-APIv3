using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;
using NotificationsApp = global::LgymApi.Application.Notifications;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class TrainerInvitationAcceptedInAppNotificationCommandHandler : global::LgymApi.BackgroundWorker.Common.IBackgroundAction<TrainerInvitationAcceptedInAppNotificationCommand>
{
    private readonly NotificationsApp.IInAppNotificationService _notificationService;
    private readonly ILogger<TrainerInvitationAcceptedInAppNotificationCommandHandler> _logger;

    public TrainerInvitationAcceptedInAppNotificationCommandHandler(
        NotificationsApp.IInAppNotificationService notificationService,
        ILogger<TrainerInvitationAcceptedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainerInvitationAcceptedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var input = new NotificationsApp.Models.CreateInAppNotificationInput(
            command.TrainerId,
            command.TraineeId,
            false,
            global::LgymApi.Resources.Messages.TrainerInvitationAccepted,
            "/trainers/dashboard",
            InAppNotificationTypes.InvitationAccepted);

        var result = await _notificationService.CreateAsync(input, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError($"Failed to create invitation-accepted notification for trainer {command.TrainerId}: {result.Error}");
        }
    }
}
