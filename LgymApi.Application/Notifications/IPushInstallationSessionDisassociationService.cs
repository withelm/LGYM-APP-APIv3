using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications;

public interface IPushInstallationSessionDisassociationService
{
    Task StageDisassociateForSessionAsync(
        Id<UserSession> sessionId,
        CancellationToken cancellationToken = default);
}
