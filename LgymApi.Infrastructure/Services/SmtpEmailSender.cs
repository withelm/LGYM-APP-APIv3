using System.Net;
using System.Net.Mail;
using System.Text;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Infrastructure.Options;

namespace LgymApi.Infrastructure.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _emailOptions;

    public SmtpEmailSender(EmailOptions emailOptions)
    {
        _emailOptions = emailOptions;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!_emailOptions.Enabled)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_emailOptions.FromAddress, _emailOptions.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        mailMessage.To.Add(message.To);

        using var smtpClient = new SmtpClient(_emailOptions.SmtpHost, _emailOptions.SmtpPort)
        {
            EnableSsl = _emailOptions.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(_emailOptions.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_emailOptions.Username, _emailOptions.Password)
        };

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }
}
