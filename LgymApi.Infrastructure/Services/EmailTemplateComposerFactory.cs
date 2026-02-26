using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;

namespace LgymApi.Infrastructure.Services;

public sealed class EmailTemplateComposerFactory : IEmailTemplateComposerFactory
{
    private readonly IReadOnlyDictionary<string, IEmailTemplateComposer> _composers;

    public EmailTemplateComposerFactory(IEnumerable<IEmailTemplateComposer> composers)
    {
        _composers = composers.ToDictionary(x => x.NotificationType, StringComparer.Ordinal);
    }

    public EmailMessage ComposeMessage(string notificationType, string payloadJson)
    {
        if (!_composers.TryGetValue(notificationType, out var composer))
        {
            throw new InvalidOperationException($"Email template composer not registered for notification type: {notificationType}");
        }

        return composer.Compose(payloadJson);
    }
}
