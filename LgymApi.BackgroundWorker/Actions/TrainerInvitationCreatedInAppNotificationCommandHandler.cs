using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;
using NotificationsApp = global::LgymApi.Application.Notifications;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class TrainerInvitationCreatedInAppNotificationCommandHandler : global::LgymApi.BackgroundWorker.Common.IBackgroundAction<TrainerInvitationCreatedInAppNotificationCommand>
{
    private readonly NotificationsApp.IInAppNotificationService _notificationService;
    private readonly ILogger<TrainerInvitationCreatedInAppNotificationCommandHandler> _logger;

    public TrainerInvitationCreatedInAppNotificationCommandHandler(
        NotificationsApp.IInAppNotificationService notificationService,
        ILogger<TrainerInvitationCreatedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainerInvitationCreatedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var input = new NotificationsApp.Models.CreateInAppNotificationInput(
            command.TraineeId,
            command.TrainerId,
            false,
            global::LgymApi.Resources.Messages.TrainerInvitationSent,
            "/athlete/relationship",
            InAppNotificationTypes.InvitationSent);

        var result = await _notificationService.CreateAsync(input, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError($"Failed to create invitation-sent notification for trainee {command.TraineeId}: {result.Error}");
        }
    }
}
