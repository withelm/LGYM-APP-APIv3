using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications.Contracts.Push;

public sealed record PushInstallationDeliveryTarget(
    Id<PushInstallation> InstallationId,
    Id<User> UserId,
    Id<UserSession> SessionId,
    long RegistrationRevision,
    string FcmToken);
