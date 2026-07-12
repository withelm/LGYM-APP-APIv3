using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class PushInstallation : EntityBase<PushInstallation>
{
    public Id<User>? UserId { get; set; }
    public Id<UserSession>? SessionId { get; set; }
    public string InstallationId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string FcmToken { get; set; } = string.Empty;
    public string? AppVersion { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string? PermissionStatus { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
    public string? DisabledReason { get; set; }
}
