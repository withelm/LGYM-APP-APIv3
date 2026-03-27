using System.Globalization;
using LgymApi.Domain.Notifications;
using System.Text.Json.Serialization;

namespace LgymApi.BackgroundWorker.Common.Notifications.Models;

public sealed class InvitationEmailPayload : IEmailPayload
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation> InvitationId { get; init; }
    public string InvitationCode { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public string TrainerName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = string.Empty;
    public string PreferredTimeZone { get; init; } = string.Empty;

    public Guid CorrelationId => (Guid)InvitationId;
    public EmailNotificationType NotificationType => Domain.Notifications.EmailNotificationTypes.TrainerInvitation;

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
