using LgymApi.Application.Repositories;

namespace LgymApi.Notifications.Application;

public interface IInAppNotificationServiceDependencies
{
    IInAppNotificationRepository InAppNotificationRepository { get; }
    IUnitOfWork UnitOfWork { get; }
    IInAppNotificationPushPublisher PushPublisher { get; }
}

internal sealed record InAppNotificationServiceDependencies : IInAppNotificationServiceDependencies
{
    public IInAppNotificationRepository InAppNotificationRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
    public IInAppNotificationPushPublisher PushPublisher { get; }

    public InAppNotificationServiceDependencies(
        IInAppNotificationRepository inAppNotificationRepository,
        IUnitOfWork unitOfWork,
        IInAppNotificationPushPublisher pushPublisher)
    {
        InAppNotificationRepository = inAppNotificationRepository;
        UnitOfWork = unitOfWork;
        PushPublisher = pushPublisher;
    }
}
