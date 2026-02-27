using System.Globalization;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Domain.Notifications;
using System.Text.Json.Serialization;

namespace LgymApi.BackgroundWorker.Common.Notifications.Models;

public sealed class InvitationEmailPayload : IEmailPayload
{
    public Guid InvitationId { get; init; }
    public string InvitationCode { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public string TrainerName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = "en-US";

    public Guid CorrelationId => InvitationId;
    public EmailNotificationType NotificationType => EmailNotificationTypes.TrainerInvitation;

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
