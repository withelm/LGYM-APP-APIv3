using System.Globalization;
using System.Text.Json;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Infrastructure.Options;
using EmailNotificationType = LgymApi.Domain.Notifications.EmailNotificationType;

namespace LgymApi.Infrastructure.Services;

public sealed class TrainerInvitationEmailTemplateComposer : EmailTemplateComposerBase, IEmailTemplateComposer
{
    public TrainerInvitationEmailTemplateComposer(EmailOptions emailOptions)
        : base(emailOptions)
    {
    }

    public EmailNotificationType NotificationType => EmailNotificationTypes.TrainerInvitation;

    public EmailMessage Compose(string payloadJson)
    {
        var payload = DeserializePayload(payloadJson);
        return ComposeTrainerInvitation(payload);
    }

    public EmailMessage ComposeTrainerInvitation(InvitationEmailPayload payload)
    {
        var culture = payload.Culture;
        var template = LoadTemplate("TrainerInvitation", culture);
        var baseUrl = EmailOptions.InvitationBaseUrl.TrimEnd('/');
        var acceptUrl = $"{baseUrl}/accept/{payload.InvitationId}";
        var rejectUrl = $"{baseUrl}/reject/{payload.InvitationId}";

        var expiresAt = payload.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{TrainerName}}"] = SanitizeTemplateValue(payload.TrainerName),
            ["{{InvitationCode}}"] = SanitizeTemplateValue(payload.InvitationCode),
            ["{{AcceptUrl}}"] = SanitizeTemplateValue(acceptUrl),
            ["{{RejectUrl}}"] = SanitizeTemplateValue(rejectUrl),
            ["{{ExpiresAt}}"] = expiresAt
        };

        var subject = Render(template.Subject, replacements);
        var body = Render(template.Body, replacements);

        return new EmailMessage
        {
            To = payload.RecipientEmail,
            Subject = subject,
            Body = body
        };
    }

    private static InvitationEmailPayload DeserializePayload(string payloadJson)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<InvitationEmailPayload>(payloadJson);
            if (payload == null)
            {
                throw new InvalidOperationException("Invitation email payload is empty.");
            }

            return payload;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to deserialize invitation email payload.", ex);
        }
    }

}
