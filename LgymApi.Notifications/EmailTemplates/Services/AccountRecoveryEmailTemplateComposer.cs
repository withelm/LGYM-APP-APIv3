using System.Globalization;
using System.Text.Json;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Notifications;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Resources;

namespace LgymApi.Infrastructure.Services;

internal sealed class AccountRecoveryEmailTemplateComposer : IEmailTemplateComposer
{
    public EmailNotificationType NotificationType => EmailNotificationTypes.AccountRecovery;

    public EmailMessage Compose(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<AccountRecoveryEmail>(payloadJson, SharedSerializationOptions.Current)
            ?? throw new InvalidOperationException("Account recovery email payload is missing.");
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(payload.CultureName);
            return new EmailMessage
            {
                To = payload.RecipientEmail,
                Subject = Emails.AccountRecoverySubject,
                Body = string.Format(CultureInfo.CurrentUICulture, Emails.AccountRecoveryBody, payload.RecoveryToken)
            };
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
