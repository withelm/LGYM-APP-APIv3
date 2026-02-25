using LgymApi.Application.Notifications.Models;

namespace LgymApi.Application.Notifications;

public interface IEmailTemplateComposer
{
    EmailMessage ComposeTrainerInvitation(InvitationEmailPayload payload);
    EmailMessage ComposeWelcome(WelcomeEmailPayload payload);
}
