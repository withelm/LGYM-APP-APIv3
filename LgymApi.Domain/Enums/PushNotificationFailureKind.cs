namespace LgymApi.Domain.Enums;

public enum PushNotificationFailureKind
{
    None = 0,
    Transient = 1,
    InvalidToken = 2,
    Permanent = 3,
    Preference = 4
}
