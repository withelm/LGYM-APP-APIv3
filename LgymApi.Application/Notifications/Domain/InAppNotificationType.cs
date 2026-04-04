namespace LgymApi.Notifications.Domain;

public sealed record InAppNotificationType(string Value)
{
    public static InAppNotificationType Define(string value) => new(value);
    public static InAppNotificationType Parse(string value) => new(value);
}
