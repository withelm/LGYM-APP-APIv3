using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Notifications;
using Microsoft.Extensions.Logging;
using NotificationsApp = global::LgymApi.Application.Notifications;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class TrainerInvitationCreatedInAppNotificationCommandHandler : global::LgymApi.BackgroundWorker.Common.IBackgroundAction<TrainerInvitationCreatedInAppNotificationCommand>
{
    private readonly NotificationsApp.IInAppNotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<TrainerInvitationCreatedInAppNotificationCommandHandler> _logger;

    public TrainerInvitationCreatedInAppNotificationCommandHandler(
        NotificationsApp.IInAppNotificationService notificationService,
        IUserRepository userRepository,
        ILogger<TrainerInvitationCreatedInAppNotificationCommandHandler> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(TrainerInvitationCreatedInAppNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var trainer = await _userRepository.FindByIdAsync(command.TrainerId, cancellationToken);
        var trainerName = string.IsNullOrWhiteSpace(trainer?.Name) ? "Trainer" : trainer.Name;

        var input = new NotificationsApp.Models.CreateInAppNotificationInput(
            command.TraineeId,
            command.TrainerId,
            $"trainer-invitation:{command.InvitationId}:sent",
            false,
            $"{trainerName} zaprasza Cię do współpracy trenerskiej.",
            $"/trainers/invitations/{command.InvitationId}",
            InAppNotificationTypes.InvitationSent);

        var result = await _notificationService.CreateAsync(input, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError($"Failed to create invitation-sent notification for trainee {command.TraineeId}: {result.Error}");
        }
    }
}
