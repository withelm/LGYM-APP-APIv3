using System.Globalization;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace LgymApi.BackgroundWorker.Common.Notifications.Models;

public sealed class PasswordRecoveryEmailPayload : IEmailPayload
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User> UserId { get; init; }
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.PasswordResetToken> TokenId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public string ResetToken { get; init; } = string.Empty;
    public string ResetUrl { get; init; } = string.Empty;
    public string CultureName { get; init; } = string.Empty;

    public Id<CorrelationScope> CorrelationId => TokenId.Rebind<CorrelationScope>();
    public EmailNotificationType NotificationType => Domain.Notifications.EmailNotificationTypes.PasswordRecovery;

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
