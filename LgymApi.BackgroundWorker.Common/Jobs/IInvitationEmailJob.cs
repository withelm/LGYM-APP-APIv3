using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Jobs;

public interface IInvitationEmailJob
{
    Task ExecuteAsync(Id<NotificationMessage> notificationId);
}
