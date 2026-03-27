using System.Globalization;
using LgymApi.Domain.Notifications;
using System.Text.Json.Serialization;

namespace LgymApi.BackgroundWorker.Common.Notifications.Models;

public sealed class WelcomeEmailPayload : IEmailPayload
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = string.Empty;

    public Guid CorrelationId => (Guid)UserId;
    public EmailNotificationType NotificationType => Domain.Notifications.EmailNotificationTypes.Welcome;

    [JsonIgnore]
    public CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(CultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("en-US");
            }
        }
    }
}
