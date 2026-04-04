using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Notifications.Domain;
using Microsoft.Extensions.Logging;
using NotificationsApp = global::LgymApi.Notifications.Application;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class TrainerInvitationRejectedInAppNotificationCommandHandler : global::LgymApi.BackgroundWorker.Common.IBackgroundAction<TrainerInvitationRejectedInAppNotificationCommand>
{
    private readonly NotificationsApp.IInAppNotificationService _notificationService;
    private readonly ILogger<TrainerInvitationRejectedInAppNotificationCommandHandler> _logger;

    public TrainerInvitationRejectedInAppNotificationCommandHandler(
        NotificationsApp.IInAppNotificationService notificationService,
        ILogger<TrainerInvitationRejectedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainerInvitationRejectedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var input = new NotificationsApp.Models.CreateInAppNotificationInput(
            command.TrainerId,
            command.TraineeId,
            false,
            global::LgymApi.Resources.Messages.TrainerInvitationRejected,
            "/trainers/dashboard",
            InAppNotificationTypes.InvitationRejected);

        var result = await _notificationService.CreateAsync(input, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError($"Failed to create invitation-rejected notification for trainer {command.TrainerId}: {result.Error}");
        }
    }
}
