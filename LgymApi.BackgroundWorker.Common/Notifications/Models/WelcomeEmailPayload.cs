using System.Globalization;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Domain.Notifications;
using System.Text.Json.Serialization;

namespace LgymApi.BackgroundWorker.Common.Notifications.Models;

public sealed class WelcomeEmailPayload : IEmailPayload
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = "en-US";

    public Guid CorrelationId => UserId;
    public EmailNotificationType NotificationType => EmailNotificationTypes.Welcome;

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
