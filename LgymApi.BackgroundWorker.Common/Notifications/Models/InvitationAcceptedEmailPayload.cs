using System.Globalization;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace LgymApi.BackgroundWorker.Common.Notifications.Models;

public sealed class InvitationAcceptedEmailPayload : IEmailPayload
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation> InvitationId { get; init; }
    public string TrainerName { get; init; } = string.Empty;
    public string TraineeName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = string.Empty;
    public string PreferredTimeZone { get; init; } = string.Empty;

    public Id<CorrelationScope> CorrelationId => InvitationId.Rebind<CorrelationScope>();
    public EmailNotificationType NotificationType => Domain.Notifications.EmailNotificationTypes.TrainerInvitationAccepted;

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
