using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications.Contracts.Push;

public sealed record PushInstallationDeliveryTarget
{
    public PushInstallationDeliveryTarget(
        Id<PushInstallation> installationId,
        Id<User> userId,
        Id<UserSession> sessionId,
        long registrationRevision)
    {
        InstallationId = installationId;
        UserId = userId;
        SessionId = sessionId;
        RegistrationRevision = registrationRevision;
    }

    internal PushInstallationDeliveryTarget(
        Id<PushInstallation> installationId,
        Id<User> userId,
        Id<UserSession> sessionId,
        long registrationRevision,
        string fcmToken)
        : this(installationId, userId, sessionId, registrationRevision)
    {
        FcmToken = fcmToken;
    }

    public Id<PushInstallation> InstallationId { get; }
    public Id<User> UserId { get; }
    public Id<UserSession> SessionId { get; }
    public long RegistrationRevision { get; }

    internal string FcmToken { get; } = string.Empty;
}
